using MissionPlannerAvalonia.ViewModels;
using Xunit;

namespace MissionPlannerAvalonia.Tests;

public class NmeaChecksumTests {
  [Theory]

  [InlineData("$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,", "47")]

  [InlineData("$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W", "6A")]
  public void Checksum_Matches_Reference_Vectors(string sentence, string expected) {
    Assert.Equal(expected, SerialOutputNMEAViewModel.GetChecksum(sentence));
  }

  [Fact]
  public void Checksum_Is_TwoHex_Uppercase_And_Order_Independent_Of_Dollar() {

    var withDollar = SerialOutputNMEAViewModel.GetChecksum("$GPVTG,054.7,T,034.4,M,005.5,N,010.2,K");
    var withoutDollar = SerialOutputNMEAViewModel.GetChecksum("GPVTG,054.7,T,034.4,M,005.5,N,010.2,K");
    Assert.Equal(withDollar, withoutDollar);
    Assert.Equal(2, withDollar.Length);
    Assert.Equal(withDollar, withDollar.ToUpperInvariant());
  }

  [Fact]
  public void Checksum_Stops_At_Star() {

    var a = SerialOutputNMEAViewModel.GetChecksum("$GPGGA,123519,4807.038,N");
    var b = SerialOutputNMEAViewModel.GetChecksum("$GPGGA,123519,4807.038,N*FF");
    Assert.Equal(a, b);
  }
}
