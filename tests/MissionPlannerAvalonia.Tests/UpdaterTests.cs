using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using MissionPlannerAvalonia.Services;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace MissionPlannerAvalonia.Tests;

public class UpdaterTests {
  private const string Base = "https://test.local/updates";
  private const string Rid = "test-rid";

  private sealed class FakeHandler : HttpMessageHandler {
    public readonly Dictionary<string, byte[]> Routes = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) {
      var url = req.RequestUri!.AbsoluteUri;
      var resp = Routes.TryGetValue(url, out var body)
          ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
          : new HttpResponseMessage(HttpStatusCode.NotFound);
      return Task.FromResult(resp);
    }
  }

  private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

  private static (Ed25519PrivateKeyParameters priv, byte[] pub) NewKey() {
    var gen = new Ed25519KeyPairGenerator();
    gen.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
    var kp = gen.GenerateKeyPair();
    return ((Ed25519PrivateKeyParameters)kp.Private, ((Ed25519PublicKeyParameters)kp.Public).GetEncoded());
  }

  private static byte[] Sign(Ed25519PrivateKeyParameters key, byte[] data) {
    var s = new Ed25519Signer();
    s.Init(true, key);
    s.BlockUpdate(data, 0, data.Length);
    return s.GenerateSignature();
  }

  private static byte[] ManifestJson(string version, params (string path, byte[] bytes)[] files) {
    var m = new {
      version,
      notes = "https://example/notes",
      files = files.Select(f => new { path = f.path, sha256 = Sha(f.bytes), size = f.bytes.LongLength })
                   .ToArray(),
    };
    return JsonSerializer.SerializeToUtf8Bytes(m);
  }

  private static string TempDir() {
    var d = Path.Combine(Path.GetTempPath(), "mp-updater-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    return d;
  }

  [Fact]
  public async Task Downloads_and_swaps_only_changed_files() {
    var (priv, pub) = NewKey();
    byte[] newApp = [1, 2, 3, 4, 5];
    byte[] newSub = [9, 8, 7];
    byte[] json = ManifestJson("2026.7.0", ("app.dll", newApp), ("sub/new.dll", newSub));

    var handler = new FakeHandler();
    handler.Routes[$"{Base}/{Rid}/manifest.json"] = json;
    handler.Routes[$"{Base}/{Rid}/manifest.sig"] =
        System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(Sign(priv, json)));
    handler.Routes[$"{Base}/{Rid}/app.dll"] = newApp;
    handler.Routes[$"{Base}/{Rid}/sub/new.dll"] = newSub;

    string install = TempDir();
    string staging = TempDir();
    File.WriteAllBytes(Path.Combine(install, "app.dll"), [0, 0, 0]);

    var engine = new UpdateEngine(new HttpClient(handler), install, Base, pub, Rid);
    var m = await engine.FetchManifestAsync();
    Assert.NotNull(m);
    Assert.Equal("2026.7.0", m!.Version);

    var changed = engine.Diff(m);
    Assert.Equal(2, changed.Count);

    await engine.DownloadAsync(changed, staging);
    engine.Apply(changed, staging);

    Assert.Equal(newApp, File.ReadAllBytes(Path.Combine(install, "app.dll")));
    Assert.Equal(newSub, File.ReadAllBytes(Path.Combine(install, "sub", "new.dll")));
  }

  [Fact]
  public async Task Rejects_tampered_manifest() {
    var (priv, pub) = NewKey();
    byte[] json = ManifestJson("2026.7.0", ("app.dll", [1, 2, 3]));
    byte[] sig = Sign(priv, json);

    byte[] tampered = (byte[])json.Clone();
    tampered[^2] ^= 0xFF;

    var handler = new FakeHandler();
    handler.Routes[$"{Base}/{Rid}/manifest.json"] = tampered;
    handler.Routes[$"{Base}/{Rid}/manifest.sig"] =
        System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(sig));

    var engine = new UpdateEngine(new HttpClient(handler), TempDir(), Base, pub, Rid);
    await Assert.ThrowsAsync<SecurityException>(() => engine.FetchManifestAsync());
  }

  [Fact]
  public async Task Download_throws_on_hash_mismatch_and_leaves_install_untouched() {
    var (priv, pub) = NewKey();
    byte[] realApp = [1, 2, 3, 4];
    byte[] json = ManifestJson("2026.7.0", ("app.dll", realApp));

    var handler = new FakeHandler();
    handler.Routes[$"{Base}/{Rid}/manifest.json"] = json;
    handler.Routes[$"{Base}/{Rid}/manifest.sig"] =
        System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(Sign(priv, json)));
    handler.Routes[$"{Base}/{Rid}/app.dll"] = [6, 6, 6];

    string install = TempDir();
    File.WriteAllBytes(Path.Combine(install, "app.dll"), [0, 0]);
    var engine = new UpdateEngine(new HttpClient(handler), install, Base, pub, Rid);

    var m = await engine.FetchManifestAsync();
    var changed = engine.Diff(m!);
    await Assert.ThrowsAsync<InvalidDataException>(() => engine.DownloadAsync(changed, TempDir()));
    Assert.Equal(new byte[] { 0, 0 }, File.ReadAllBytes(Path.Combine(install, "app.dll")));
  }

  [Fact]
  public void Apply_rolls_back_when_a_staged_file_is_missing() {
    var (_, pub) = NewKey();
    string install = TempDir();
    string staging = TempDir();
    byte[] origA = [10, 11];
    File.WriteAllBytes(Path.Combine(install, "a.dll"), origA);
    File.WriteAllBytes(Path.Combine(install, "b.dll"), [20, 21]);
    File.WriteAllBytes(Path.Combine(staging, "a.dll"), [99]);

    var engine = new UpdateEngine(new HttpClient(new FakeHandler()), install, Base, pub, Rid);
    var changed = new List<UpdateEngine.ManifestFile> {
      new("a.dll", "x", 1),
      new("b.dll", "x", 1),
    };

    Assert.ThrowsAny<Exception>(() => engine.Apply(changed, staging));
    Assert.Equal(origA, File.ReadAllBytes(Path.Combine(install, "a.dll")));
  }

  [Theory]
  [InlineData("2026.7.0", "2026.6.9", true)]
  [InlineData("2026.7.1", "2026.7.0", true)]
  [InlineData("2026.7.0", "2026.7.0", false)]
  [InlineData("2026.6.0", "2026.7.0", false)]
  [InlineData("v2026.7.0", "2026.7.0-beta", false)]
  public void IsNewer_compares_calver(string remote, string local, bool expected) {
    Assert.Equal(expected, UpdateEngine.IsNewer(remote, local));
  }
}
