// Copyright (c) HardwareMonitor. MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using HardwareMonitorExtension.Localization;
using HardwareMonitorExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HardwareMonitorExtension.Pages;

/// <summary>
/// Stable, native Command Palette form for editing a single Dock reading.
/// Available from the Dock row context menu and the sensor-configuration list.
/// </summary>
internal sealed partial class DockAppearancePage : ContentPage
{
    private readonly DockAppearanceForm _form;

    public DockAppearancePage(DockItemAppearanceStore store, string sensorId)
    {
        Title = Text.Get("DockAppearanceEdit");
        Name = Text.Get("DockAppearanceEdit");
        Icon = new IconInfo("🎨");
        _form = new DockAppearanceForm(store, sensorId);
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class DockAppearanceForm : FormContent
{
    private readonly DockItemAppearanceStore _store;
    private readonly string _sensorId;
    private static readonly string[] Presets =
    [
        "🌡", "📊", "💻", "🖥", "🧠", "💾", "🌀", "⚡", "📈",
        "🔋", "🔥", "⚠", "🎮", "🧊", "💡", "📡",
    ];

    internal DockAppearanceForm(DockItemAppearanceStore store, string sensorId)
    {
        _store = store;
        _sensorId = sensorId;

        var choices = new List<object> { new { title = Text.Get("DockAppearanceAuto"), value = "auto" } };
        choices.AddRange(Presets.Select(x => (object)new { title = x, value = x }));
        TemplateJson = JsonSerializer.Serialize(new
        {
            type = "AdaptiveCard",
            version = "1.6",
            body = new object[]
            {
                new { type = "TextBlock", text = Text.Get("DockAppearanceDescription"), wrap = true },
                new { type = "Input.ChoiceSet", id = "PresetIcon",
                    label = Text.Get("DockAppearancePresetIcon"), style = "compact",
                    choices, value = "${PresetIcon}" },
                new { type = "Input.Text", id = "CustomIcon",
                    label = Text.Get("DockAppearanceCustomIcon"),
                    placeholder = Text.Get("DockAppearanceCustomIconHint"),
                    maxLength = 24, value = "${CustomIcon}" },
                new { type = "Input.Text", id = "Title",
                    label = Text.Get("DockAppearanceTitle"),
                    placeholder = Text.Get("DockAppearanceTitleHint"),
                    maxLength = 64, value = "${Title}" },
                new { type = "Input.Text", id = "Subtitle",
                    label = Text.Get("DockAppearanceSubtitle"),
                    placeholder = Text.Get("DockAppearanceSubtitleHint"),
                    maxLength = 128, value = "${Subtitle}" },
                new { type = "TextBlock", text = Text.Get("DockAppearanceCompactHint"), wrap = true, isSubtle = true }
            },
            actions = new object[]
            {
                new { type = "Action.Submit", title = Text.Get("DockAppearanceSave"), data = new { action = "save" } },
                new { type = "Action.Submit", title = Text.Get("DockAppearanceReset"), data = new { action = "reset" } }
            }
        });

        var current = store.Get(sensorId);
        var preset = Presets.Contains(current.Icon, StringComparer.Ordinal) ? current.Icon : "auto";
        DataJson = JsonSerializer.Serialize(new
        {
            PresetIcon = preset,
            CustomIcon = preset == "auto" ? current.Icon : "",
            Title = current.Title,
            Subtitle = current.Subtitle,
        });
    }

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            var input = JsonNode.Parse(payload)?.AsObject();
            if (input is null) return CommandResult.KeepOpen();
            if (GetString(input, "action") == "reset")
            {
                _store.Reset(_sensorId);
                return CommandResult.GoBack();
            }

            var icon = GetString(input, "CustomIcon");
            if (string.IsNullOrWhiteSpace(icon))
            {
                var preset = GetString(input, "PresetIcon");
                icon = preset == "auto" ? "" : preset;
                if (icon.Length > 0 && !Presets.Contains(icon, StringComparer.Ordinal))
                    return CommandResult.KeepOpen();
            }

            var appearance = new DockItemAppearance(
                icon, GetString(input, "Title"), GetString(input, "Subtitle"));
            _store.Set(_sensorId, appearance);
            return CommandResult.GoBack();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Leave the form open on invalid input or storage failure.
            return CommandResult.KeepOpen();
        }
    }

    private static string GetString(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<string>(out var s) ? s ?? "" : "";
}
