using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class ReleaseItemTests
{
    [Theory]
    [InlineData("Saatli hatırlatma: işe saat ve en çok iki hatırlatma", "Saatli hatırlatma", "İşe saat ve en çok iki hatırlatma")]
    [InlineData("Sağ panelde Taşı altta sabit; aralarında çizgi", "Sağ panelde Taşı altta sabit", "Aralarında çizgi")]
    [InlineData("Takvimdeyken İşler düğmesi doğrudan Bugün sayfasını açar", "Takvimdeyken İşler düğmesi doğrudan Bugün sayfasını açar", "")]
    public void Splits_AtFirstColonOrSemicolon(string item, string head, string detail)
    {
        var split = ReleaseItem.Of(item);
        Assert.Equal(head, split.Head);
        Assert.Equal(detail, split.Detail);
    }

    [Fact]
    public void LongLead_StaysOneLine()
    {
        var item = "Bitmeyen ama alt işi biten işler ilerledi diye sayılır ve başlıklarda bu da yazılır ve takvimde de dahası da var: ayrıntı";
        Assert.Equal(item, ReleaseItem.Of(item).Head);
        Assert.Equal("", ReleaseItem.Of(item).Detail);
    }

    [Fact]
    public void FullStop_BeforeANewSentence_Splits()
    {
        var split = ReleaseItem.Of("Yeni iş kutusu dışarı basınca küçülür. Kutu takvimden gün seçer");
        Assert.Equal("Yeni iş kutusu dışarı basınca küçülür", split.Head);
        Assert.Equal("Kutu takvimden gün seçer", split.Detail);
    }

    [Fact]
    public void Time_IsNotASplit()
    {
        var split = ReleaseItem.Of("Yazarken \"toplantı 14:00\" saati alır");
        Assert.Equal("Yazarken \"toplantı 14:00\" saati alır", split.Head);
    }
}
