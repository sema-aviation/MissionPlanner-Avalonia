using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigSecureViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private uint _sequence = 1;
  private byte[]? _sessionKey;

  public ObservableCollection<SecureKeySlot> Keys { get; } = new();

  [ObservableProperty]
  private string _log = "";

  [ObservableProperty]
  private string _sessionKeyText = "(none)";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  private byte TargetSystem => _comPort.MAV.sysid;
  private byte TargetComponent => _comPort.MAV.compid;

  [RelayCommand]
  private async Task GetSessionKey() {
    if (!Ensure()) {
      return;
    }

    AppendLog("Requesting session key...");
    var reply = await Task.Run(() =>
        SendSecureCommand((uint)MAVLink.SECURE_COMMAND_OP.SECURE_COMMAND_GET_SESSION_KEY, Array.Empty<byte>(), 0));

    if (reply == null) {
      AppendLog("No reply (timeout).");
      return;
    }

    if (reply.Value.result == (byte)MAVLink.MAV_RESULT.ACCEPTED && reply.Value.data_length > 0) {
      _sessionKey = reply.Value.data.Take(reply.Value.data_length).ToArray();
      SessionKeyText = BitConverter.ToString(_sessionKey).Replace("-", "");
      AppendLog($"Session key ({_sessionKey.Length} bytes): {SessionKeyText}");
    } else {
      AppendLog($"GET_SESSION_KEY {(MAVLink.MAV_RESULT)reply.Value.result}");
    }
  }

  [RelayCommand]
  private async Task GetKeys() {
    if (!Ensure()) {
      return;
    }

    Dispatcher.UIThread.Post(() => Keys.Clear());
    AppendLog("Requesting public keys...");

    await Task.Run(() => {
      for (byte idx = 0; idx < 10; idx++) {
        // request 1 key at a time: data = [key_idx, num_keys]
        var reply = SendSecureCommand((uint)MAVLink.SECURE_COMMAND_OP.SECURE_COMMAND_GET_PUBLIC_KEYS,
            new byte[] { idx, 1 }, 0);

        if (reply == null) {
          AppendLog($"Slot {idx}: no reply (timeout). Stopping.");
          break;
        }

        if (reply.Value.result != (byte)MAVLink.MAV_RESULT.ACCEPTED) {
          AppendLog($"Slot {idx}: {(MAVLink.MAV_RESULT)reply.Value.result}. Stopping.");
          break;
        }

        var data = reply.Value.data.Take(reply.Value.data_length).ToArray();
        if (data.Length == 0) {
          AppendLog($"Slot {idx}: empty. Stopping.");
          break;
        }

        // reply: [key_idx][32-byte key...]
        var keyBytes = data.Skip(1).ToArray();
        var hex = BitConverter.ToString(keyBytes).Replace("-", "");
        var slot = idx;
        Dispatcher.UIThread.Post(() => Keys.Add(new SecureKeySlot(slot, hex)));
        AppendLog($"Slot {idx}: {hex}");
      }
    });

    AppendLog("Get keys done.");
  }

  public async Task SetKeyFromFileAsync(string path) {
    if (!Ensure()) {
      return;
    }

    byte[] keyBytes;
    try {
      keyBytes = LoadPublicKey(path);
    } catch (Exception ex) {
      AppendLog("Failed to read key: " + ex.Message);
      return;
    }

    if (keyBytes.Length != 32) {
      AppendLog($"Expected a 32-byte Ed25519 public key, got {keyBytes.Length} bytes.");
      return;
    }

    // SET_PUBLIC_KEYS data = [key_idx][key bytes...]. New keys appended at index = current count.
    var idx = (byte)Keys.Count;
    var data = new byte[1 + keyBytes.Length];
    data[0] = idx;
    Array.Copy(keyBytes, 0, data, 1, keyBytes.Length);

    AppendLog($"Setting public key at slot {idx} ({Path.GetFileName(path)})...");
    await SendSignedAsync((uint)MAVLink.SECURE_COMMAND_OP.SECURE_COMMAND_SET_PUBLIC_KEYS, data);
  }

  [RelayCommand]
  private async Task RemoveKeys() {
    if (!Ensure()) {
      return;
    }

    // REMOVE_PUBLIC_KEYS data = [key_idx, num_keys]. Remove all known keys.
    var num = (byte)Math.Max(1, Keys.Count);
    AppendLog($"Removing {num} public key(s) starting at slot 0...");
    await SendSignedAsync((uint)MAVLink.SECURE_COMMAND_OP.SECURE_COMMAND_REMOVE_PUBLIC_KEYS,
        new byte[] { 0, num });
  }

  private async Task SendSignedAsync(uint op, byte[] data) {
    // SET/REMOVE require the command be signed over
    // sequence(LE) | operation(LE) | data | session_key with a private key whose public
    // key is already trusted by the target. No Ed25519 signer is available in the reachable
    // assemblies, so the command is sent unsigned (sig_length = 0); the target will reject it
    // if it requires a signature. This mirrors the wire format exactly without fabricating a
    // signature.
    if (_sessionKey == null) {
      AppendLog("No session key yet — fetching one first.");
      await GetSessionKey();
    }

    var reply = await Task.Run(() => SendSecureCommand(op, data, 0));
    if (reply == null) {
      AppendLog("No reply (timeout).");
      return;
    }

    var result = (MAVLink.MAV_RESULT)reply.Value.result;
    AppendLog($"{(MAVLink.SECURE_COMMAND_OP)op} -> {result}");
    if (result != MAVLink.MAV_RESULT.ACCEPTED) {
      AppendLog("Note: SET/REMOVE must be signed with a trusted private key. " +
          "Signing is not available in this build, so the autopilot rejected the unsigned command.");
    } else {
      await GetKeys();
    }
  }

  private MAVLink.mavlink_secure_command_reply_t? SendSecureCommand(uint operation, byte[] data, byte sigLength) {
    var seq = _sequence++;
    var payload = new byte[220];
    Array.Copy(data, 0, payload, 0, Math.Min(data.Length, payload.Length));

    var req = new MAVLink.mavlink_secure_command_t {
      sequence = seq,
      operation = operation,
      target_system = TargetSystem,
      target_component = TargetComponent,
      data_length = (byte)data.Length,
      sig_length = sigLength,
      data = payload,
    };

    MAVLink.mavlink_secure_command_reply_t? result = null;
    using var got = new ManualResetEventSlim(false);

    var sub = _comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.SECURE_COMMAND_REPLY, msg => {
      var reply = msg.ToStructure<MAVLink.mavlink_secure_command_reply_t>();
      if (reply.sequence == seq && reply.operation == operation) {
        result = reply;
        got.Set();
      }
      return true;
    }, TargetSystem, TargetComponent);

    try {
      var deadline = DateTime.Now.AddSeconds(3);
      var next = DateTime.MinValue;
      while (!got.IsSet && DateTime.Now < deadline) {
        if (DateTime.Now > next) {
          _comPort.generatePacket((int)MAVLink.MAVLINK_MSG_ID.SECURE_COMMAND, req, TargetSystem, TargetComponent);
          next = DateTime.Now.AddSeconds(1);
        }
        got.Wait(50);
      }
    } finally {
      _comPort.UnSubscribeToPacketType(sub);
    }

    return result;
  }

  private static byte[] LoadPublicKey(string path) {
    var text = File.ReadAllText(path).Trim();
    if (text.Contains("PUBLIC_KEYV1")) {
      text = text.Replace("PUBLIC_KEYV1:", "").Trim();
      return Convert.FromBase64String(text);
    }

    if (text.Contains("BEGIN")) {
      // PEM-ish: strip header/footer lines and base64-decode the body.
      var body = string.Concat(text
          .Split('\n')
          .Where(l => !l.Contains("BEGIN") && !l.Contains("END"))
          .Select(l => l.Trim()));
      var decoded = Convert.FromBase64String(body);
      // Ed25519 SubjectPublicKeyInfo is 44 bytes; the raw key is the trailing 32.
      return decoded.Length > 32 ? decoded.Skip(decoded.Length - 32).ToArray() : decoded;
    }

    // assume base64 raw key
    try {
      return Convert.FromBase64String(text);
    } catch {
      return File.ReadAllBytes(path);
    }
  }

  private bool Ensure() {
    if (!IsConnected) {
      AppendLog("Not connected — open a link first.");
      return false;
    }
    return true;
  }

  private void AppendLog(string line) {
    Dispatcher.UIThread.Post(() => Log += $"{DateTime.Now:HH:mm:ss}  {line}\n");
  }
}

public partial class SecureKeySlot : ObservableObject {
  public SecureKeySlot(int index, string key) {
    Index = index;
    Key = key;
  }

  public int Index { get; }

  [ObservableProperty]
  private string _key;
}
