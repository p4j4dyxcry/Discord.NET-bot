using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TsDiscordBot.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace TsDiscordBot.Tests;

public class AnonymousProfileProviderTests
{
    [Fact]
    public void IndexZeroReturnsChris()
    {
        var profile = AnonymousProfileProvider.GetProfile(0);
        Assert.Equal("クリス", profile.Name);
    }

    [Fact]
    public void DiscriminatorFormatsToFourDigits()
    {
        Assert.Equal("0000", AnonymousProfileProvider.GetDiscriminator(0));
        Assert.Equal("0256", AnonymousProfileProvider.GetDiscriminator(256));
    }

    [Fact]
    public void SameBaseNameGetsDifferentDiscriminators()
    {
        var profile1 = AnonymousProfileProvider.GetProfile(0);
        var profile2 = AnonymousProfileProvider.GetProfile(256);
        Assert.Equal(profile1.Name, profile2.Name);
        Assert.NotEqual(
            AnonymousProfileProvider.GetDiscriminator(0),
            AnonymousProfileProvider.GetDiscriminator(256)
        );
    }

    [Fact]
    public void Generate_Html_Gallery_Without_Download()
    {
        // Profiles配列を取得
        var profilesField = typeof(AnonymousProfileProvider)
            .GetField("Profiles", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(profilesField);

        var profiles = (AnonymousProfile[])profilesField!.GetValue(null)!;
        Assert.NotEmpty(profiles);

        // 出力先
        var rootDir = Path.Combine(Directory.GetCurrentDirectory(), "AnonymousProfilesPreview");
        Directory.CreateDirectory(rootDir);
        var htmlPath = Path.Combine(rootDir, "index.html");

        // HTMLを生成
        var html = BuildHtml(profiles);
        File.WriteAllText(htmlPath, html, new UTF8Encoding(false));

        Assert.True(File.Exists(htmlPath), "HTMLファイルが生成されていません");
    }

    private static string BuildHtml(AnonymousProfile[] profiles)
    {
        var figures = profiles.Select(p =>
        {
            var nameEsc = System.Net.WebUtility.HtmlEncode(p.Name);
            var urlEsc  = System.Net.WebUtility.HtmlEncode(p.AvatarUrl ?? "");
            var fileEsc = Path.GetFileName(p.AvatarUrl ?? "");

            return $"""
                <figure>
                  <a href="{urlEsc}" target="_blank" rel="noopener">
                    <img src="{urlEsc}" alt="{nameEsc}" loading="lazy">
                  </a>
                  <figcaption>
                    <div class="name">{nameEsc}</div>
                    <div class="file">{fileEsc}</div>
                  </figcaption>
                </figure>
                """;
        });

        return """
            <!doctype html>
            <html lang="ja">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Anonymous Profiles Preview</title>
              <style>
                body { font-family: sans-serif; margin: 16px; }
                h1 { margin-bottom: 12px; font-size: 20px; }
                .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 12px; }
                figure { margin: 0; padding: 8px; border: 1px solid #ccc; border-radius: 8px; text-align: center; }
                img { max-width: 100%; height: auto; border-radius: 6px; }
                .name { font-weight: bold; margin-top: 6px; }
                .file { font-size: 12px; color: #666; }
              </style>
            </head>
            <body>
              <h1>Anonymous Profiles Preview</h1>
              <div class="grid">
            """ + string.Join(Environment.NewLine, figures) + """
              </div>
            </body>
            </html>
            """;
    }
}

