using System.Text;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Xunit;

namespace MissionPlannerAvalonia.Tests;

public class SecureCommandSignTests {
  private static Ed25519PrivateKeyParameters Key() {

    var seed = new byte[32];
    for (int i = 0; i < seed.Length; i++) {
      seed[i] = (byte)(i + 1);
    }
    return new Ed25519PrivateKeyParameters(seed, 0);
  }

  [Fact]
  public void Signature_Verifies_Against_Public_Key() {
    var key = Key();
    var pub = key.GeneratePublicKey();
    uint seq = 7, op = 3;
    var data = Encoding.ASCII.GetBytes("pubkey-slot-0");
    var session = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 };

    var sig = ConfigSecureViewModel.MakeSignature(key, seq, op, data, session);
    Assert.Equal(64, sig.Length);

    var msg = ConfigSecureViewModel.SecureCommandMessage(seq, op, data, session);
    var v = new Ed25519Signer();
    v.Init(forSigning: false, pub);
    v.BlockUpdate(msg, 0, msg.Length);
    Assert.True(v.VerifySignature(sig), "signature must verify against the trusted public key");
  }

  [Fact]
  public void Message_Layout_Is_LittleEndian_Seq_Op_Data_Session() {
    var msg = ConfigSecureViewModel.SecureCommandMessage(
        0x04030201, 0x08070605, new byte[] { 0xAA, 0xBB }, new byte[] { 0xCC });
    Assert.Equal(
        new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xAA, 0xBB, 0xCC }, msg);
  }

  [Fact]
  public void Tampered_Data_Fails_Verification() {
    var key = Key();
    var pub = key.GeneratePublicKey();
    var sig = ConfigSecureViewModel.MakeSignature(key, 1, 1, new byte[] { 1, 2, 3 }, null);

    var tampered = ConfigSecureViewModel.SecureCommandMessage(1, 1, new byte[] { 1, 2, 4 }, null);
    var v = new Ed25519Signer();
    v.Init(forSigning: false, pub);
    v.BlockUpdate(tampered, 0, tampered.Length);
    Assert.False(v.VerifySignature(sig));
  }
}
