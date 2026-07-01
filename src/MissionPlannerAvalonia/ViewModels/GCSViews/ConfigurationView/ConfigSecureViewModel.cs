using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigSecureViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private uint _sequence = 1;
  private byte[]? _sessionKey;

  private Ed25519PrivateKeyParameters? _signingKey;

  public ObservableCollection<SecureKeySlot> Keys { get; } = new();

  [ObservableProperty]
  private string _log = "";

  [ObservableProperty]
  private string _sessionKeyText = "(none)";

  [ObservableProperty]
  private string _signingKeyText = "(none)";

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
        SendSecureCommand((uint)MAVLink.SECURE_COMMAND_OP.SECURE_COMMAND_GET_SESSION_KEY, Array.Empty<byte>(), sign: false));

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

        var reply = SendSecureCommand((uint)MAVLink.SECURE_COMMAND_OP.SECURE_COMMAND_GET_PUBLIC_KEYS,
            new byte[] { idx, 1 }, sign: false);

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

    var num = (byte)Math.Max(1, Keys.Count);
    AppendLog($"Removing {num} public key(s) starting at slot 0...");
    await SendSignedAsync((uint)MAVLink.SECURE_COMMAND_OP.SECURE_COMMAND_REMOVE_PUBLIC_KEYS,
        new byte[] { 0, num });
  }

  public async Task LoadSigningKeyFromFileAsync(string path) {
    if (!Ensure()) {
      return;
    }

    try {
      _signingKey = await Task.Run(() => LoadPrivateKey(path));
      var pub = Convert.ToBase64String(_signingKey.GeneratePublicKey().GetEncoded());
      SigningKeyText = pub;
      AppendLog($"Loaded signing key ({Path.GetFileName(path)}). Public key: {pub}");
    } catch (Exception ex) {
      _signingKey = null;
      SigningKeyText = "(none)";
      AppendLog("Failed to load signing key: " + ex.Message);
    }
  }

  private async Task SendSignedAsync(uint op, byte[] data) {

    if (_signingKey == null) {
      AppendLog("No signing key loaded — use \"Load Signing Key…\" to load a trusted private key first.");
      return;
    }

    if (_sessionKey == null) {
      AppendLog("No session key yet — fetching one first.");
      await GetSessionKey();
      if (_sessionKey == null) {
        AppendLog("Could not obtain a session key; aborting signed command.");
        return;
      }
    }

    var reply = await Task.Run(() => SendSecureCommand(op, data, sign: true));
    if (reply == null) {
      AppendLog("No reply (timeout).");
      return;
    }

    var result = (MAVLink.MAV_RESULT)reply.Value.result;
    AppendLog($"{(MAVLink.SECURE_COMMAND_OP)op} -> {result}");
    if (result != MAVLink.MAV_RESULT.ACCEPTED) {
      AppendLog("Command rejected. The loaded signing key's public key must already be a trusted "
          + "key on the autopilot's bootloader for the signature to be accepted.");
    } else {
      await GetKeys();
    }
  }

  private byte[] MakeSignature(uint seq, uint operation, byte[] data) =>
      MakeSignature(_signingKey!, seq, operation, data, _sessionKey);

  public static byte[] SecureCommandMessage(uint seq, uint operation, byte[] data, byte[]? sessionKey) {
    var msg = new List<byte>(8 + data.Length + (sessionKey?.Length ?? 0));
    msg.AddRange(LittleEndian(seq));
    msg.AddRange(LittleEndian(operation));
    msg.AddRange(data);
    if (sessionKey != null) {
      msg.AddRange(sessionKey);
    }
    return msg.ToArray();
  }

  public static byte[] MakeSignature(
      Ed25519PrivateKeyParameters key, uint seq, uint operation, byte[] data, byte[]? sessionKey) {
    var bytes = SecureCommandMessage(seq, operation, data, sessionKey);
    var signer = new Ed25519Signer();
    signer.Init(forSigning: true, key);
    signer.BlockUpdate(bytes, 0, bytes.Length);
    return signer.GenerateSignature();
  }

  private static byte[] LittleEndian(uint v) => new byte[] {
    (byte)(v & 0xff), (byte)((v >> 8) & 0xff), (byte)((v >> 16) & 0xff), (byte)((v >> 24) & 0xff),
  };

  private MAVLink.mavlink_secure_command_reply_t? SendSecureCommand(uint operation, byte[] data, bool sign) {
    var seq = _sequence++;

    var sig = sign ? MakeSignature(seq, operation, data) : Array.Empty<byte>();
    var sigLength = (byte)sig.Length;

    var payload = new byte[220];
    Array.Copy(data, 0, payload, 0, Math.Min(data.Length, payload.Length));
    if (sigLength > 0) {

      Array.Copy(sig, 0, payload, data.Length, Math.Min(sig.Length, payload.Length - data.Length));
    }

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

      var body = string.Concat(text
          .Split('\n')
          .Where(l => !l.Contains("BEGIN") && !l.Contains("END"))
          .Select(l => l.Trim()));
      var decoded = Convert.FromBase64String(body);

      return decoded.Length > 32 ? decoded.Skip(decoded.Length - 32).ToArray() : decoded;
    }

    try {
      return Convert.FromBase64String(text);
    } catch {
      return File.ReadAllBytes(path);
    }
  }

  private static Ed25519PrivateKeyParameters LoadPrivateKey(string path) {
    var text = File.ReadAllText(path).Trim();

    if (text.Contains("PRIVATE_KEYV1")) {
      var b64 = text.Replace("PRIVATE_KEYV1:", "").Trim();
      var seed = Convert.FromBase64String(b64);

      return new Ed25519PrivateKeyParameters(seed, 0);
    }

    if (text.Contains("BEGIN")) {
      var pr = new PemReader(new StringReader(text));
      var obj = pr.ReadObject();
      return obj switch {
        AsymmetricCipherKeyPair kp => (Ed25519PrivateKeyParameters)kp.Private,
        Ed25519PrivateKeyParameters pk => pk,
        _ => throw new Exception("PEM did not contain an Ed25519 private key."),
      };
    }

    var raw = Convert.FromBase64String(text);
    if (raw.Length > 32) {
      raw = raw.Skip(raw.Length - 32).ToArray();
    }
    return new Ed25519PrivateKeyParameters(raw, 0);
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
