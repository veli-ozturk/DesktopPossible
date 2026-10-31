using Desktop_Frames;
using Shouldly;
using Xunit;

namespace DesktopPossible.Tests;

public class UiNamesTests
{
    [Theory]
    [InlineData("Red", "Kırmızı")]
    [InlineData("Default", "Varsayılan")]
    [InlineData("Productivity", "Verimlilik")]
    [InlineData("SomethingUnknown", "SomethingUnknown")]
    public void Display_TranslatesKnownValuesAndPassesOthersThrough(string value, string expected)
    {
        UiNames.Display(value).ShouldBe(expected);
    }

    [Fact]
    public void Profile_OnlyRenamesTheDefaultProfile()
    {
        UiNames.Profile("Default").ShouldBe("Varsayılan");
        UiNames.Profile("Red").ShouldBe("Red");
    }
}
