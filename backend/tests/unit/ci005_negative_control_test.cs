using Xunit;

namespace CurioDistrict.Tests.Unit.Ci;

public sealed class Ci005NegativeControlTest
{
    [Fact]
    public void DeliberateRemoteFailureForApprovedCiEvidence()
    {
        Assert.True(false, "Approved Sprint003 deliberate CI red control; corrected in next revision");
    }
}
