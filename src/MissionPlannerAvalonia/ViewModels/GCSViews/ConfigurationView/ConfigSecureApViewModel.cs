using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Utilities;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigSecureApViewModel : ViewModelBase {
  private AsymmetricCipherKeyPair? _keyPair;

  [ObservableProperty]
  private string _publicKeyText = "";

  [ObservableProperty]
  private string _bootloaderPath = "";

  [ObservableProperty]
  private string _firmwarePath = "";

  [ObservableProperty]
  private string _log = "";

  public void GenerateKey(string pemSavePath) {
    try {
      _keyPair = SignedFW.GenerateKey();

      TextWriter textWriter = new StringWriter();
      PemWriter pemWriter = new PemWriter(textWriter);
      pemWriter.WriteObject(_keyPair);
      pemWriter.Writer.Flush();
      var privatekey = pemWriter.Writer.ToString();

      File.WriteAllText(pemSavePath, privatekey);
      File.WriteAllText(pemSavePath.Replace(".pem", "_private_key.dat"),
          "PRIVATE_KEYV1:" + Convert.ToBase64String(((Ed25519PrivateKeyParameters)_keyPair.Private).GetEncoded()));
      File.WriteAllText(pemSavePath.Replace(".pem", "_public_key.dat"),
          "PUBLIC_KEYV1:" + Convert.ToBase64String(((Ed25519PublicKeyParameters)_keyPair.Public).GetEncoded()));

      PublicKeyText = Convert.ToBase64String(((Ed25519PublicKeyParameters)_keyPair.Public).GetEncoded());
      AppendLog("Key generated. Protect your private key, if lost there is no method to get it back.");
    } catch (Exception ex) {
      AppendLog("Generate key failed: " + ex.Message);
    }
  }

  public void LoadPrivateKey(string path) {
    try {
      var pem = File.ReadAllText(path);
      if (pem.Contains("PRIVATE_KEYV1")) {
        pem = pem.Replace("PRIVATE_KEYV1:", "");
        var keyap = Convert.FromBase64String(pem.Trim());
        _keyPair = SignedFW.GenerateKey(keyap);
      } else {
        PemReader pr = new PemReader(new StringReader(pem));
        var key = (Ed25519PrivateKeyParameters)pr.ReadObject();
        _keyPair = new AsymmetricCipherKeyPair(key.GeneratePublicKey(), key);
      }
      PublicKeyText = Convert.ToBase64String(((Ed25519PublicKeyParameters)_keyPair.Public).GetEncoded());
      AppendLog("Private key loaded.");
    } catch (Exception ex) {
      AppendLog("Load private key failed: " + ex.Message);
    }
  }

  public void SignBootloader(string binPath) {
    if (_keyPair == null) {
      AppendLog("Load or generate a key first.");
      return;
    }
    try {
      BootloaderPath = binPath;
      var ms = SignedFW.CreateSignedBL(_keyPair, binPath);
      var outPath = Path.Combine(Path.GetDirectoryName(binPath)!,
          Path.GetFileNameWithoutExtension(binPath) + "-signed.bin");
      File.WriteAllBytes(outPath, ms);
      AppendLog("Signed bootloader written: " + outPath);
    } catch (Exception ex) {
      AppendLog("Sign bootloader failed: " + ex.Message);
    }
  }

  public void SignFirmware(string apjPath) {
    if (_keyPair == null) {
      AppendLog("Load or generate a key first.");
      return;
    }
    try {
      FirmwarePath = apjPath;
      var output = SignedFW.CreateSignedAPJ(_keyPair, apjPath);
      var outPath = Path.Combine(Path.GetDirectoryName(apjPath)!,
          Path.GetFileNameWithoutExtension(apjPath) + "-signed.apj");
      File.WriteAllBytes(outPath, output);
      AppendLog("Signed firmware written: " + outPath);
    } catch (Exception ex) {
      AppendLog("Sign firmware failed: " + ex.Message);
    }
  }

  private void AppendLog(string line) {
    Log += $"{DateTime.Now:HH:mm:ss}  {line}\n";
  }
}
