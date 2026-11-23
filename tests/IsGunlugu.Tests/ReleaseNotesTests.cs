using IsGunlugu.Core;

namespace IsGunlugu.Tests;

public sealed class ReleaseNotesTests
{
    private const string Sample = """
        # Changelog

        Giriş yazısı.

        ## [Unreleased]

        ### Added

        - henüz çıkmamış iş

        ## [1.1.0] - 2026-11-04

        ### Added

        - ilk madde
        - ikinci madde

        ### Fixed

        - işte bir düzeltme

        ## 1.0.0

        ### Removed

        - eski düğme
        """;

    [Fact]
    public void Reads_versions_newest_first_and_leaves_out_unreleased()
    {
        var notes = ReleaseNotes.Parse(Sample);

        Assert.Equal(new[] { "1.1.0", "1.0.0" }, notes.Select(n => n.Version));
        Assert.DoesNotContain(notes.SelectMany(n => n.Sections).SelectMany(s => s.Items), i => i.Contains("henüz"));
    }

    [Fact]
    public void Reads_the_date_when_there_is_one()
    {
        var notes = ReleaseNotes.Parse(Sample);

        Assert.Equal(new DateTime(2026, 11, 4), notes[0].Date);
        Assert.Equal("1.1.0 · 4 Kasım 2026", notes[0].Title);
        Assert.Null(notes[1].Date);
        Assert.Equal("1.0.0", notes[1].Title);
    }

    [Fact]
    public void Shows_sections_in_turkish_and_items_as_sentences()
    {
        var note = ReleaseNotes.Parse(Sample)[0];

        Assert.Equal(new[] { "Yeni", "Düzeltilen" }, note.Sections.Select(s => s.Title));
        Assert.Equal(new[] { "İlk madde", "İkinci madde" }, note.Sections[0].Items);
        Assert.Equal("İşte bir düzeltme", note.Sections[1].Items[0]);
    }

    [Fact]
    public void Handles_windows_line_endings()
    {
        var notes = ReleaseNotes.Parse(Sample.Replace("\r\n", "\n").Replace("\n", "\r\n"));

        Assert.Equal(2, notes.Count);
        Assert.Equal(2, notes[0].Sections[0].Items.Count);
    }

    [Fact]
    public void Hides_the_build_number_at_the_end_of_an_item()
    {
        var notes = ReleaseNotes.Parse("## [0.8.5] - 2026-11-20\n\n### Fixed\n\n- kutu kapanıyor [0.8.5.3]\n- [1] ile başlayan madde\n");

        Assert.Equal("0.8.5", notes[0].Version);
        Assert.Equal(new[] { "Kutu kapanıyor", "[1] ile başlayan madde" }, notes[0].Sections[0].Items);
    }

    [Fact]
    public void Empty_text_has_no_notes()
    {
        Assert.Empty(ReleaseNotes.Parse(""));
    }
}
