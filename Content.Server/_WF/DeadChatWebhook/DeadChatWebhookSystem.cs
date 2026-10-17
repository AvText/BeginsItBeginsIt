using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._WF.DeadChatWebhook;

/// <summary>
///     Raised by the chat system after a dead chat message is sent.
/// </summary>
public readonly record struct DeadChatSentEvent(string Author, string Message);

/// <summary>
///     Copies dead chat to a Discord webhook. One way: nothing comes back from Discord.
/// </summary>
public sealed class DeadChatWebhookSystem : EntitySystem
{
    private const int MaxContentLength = 2000;
    private const int MaxUsernameLength = 80;

    [Dependency] private DiscordWebhook _discord = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private string _url = string.Empty;
    private WebhookIdentifier? _identifier;

    public override void Initialize()
    {
        SubscribeLocalEvent<DeadChatSentEvent>(OnDeadChat);
        Subs.CVar(_cfg, CCVars.DeadChatWebhook, OnUrlChanged, true);
    }

    private void OnUrlChanged(string url)
    {
        _url = url;
        _identifier = null;
    }

    private void OnDeadChat(DeadChatSentEvent ev)
    {
        if (string.IsNullOrWhiteSpace(_url))
            return;

        _ = SendAsync(_url, ev);
    }

    private async Task SendAsync(string url, DeadChatSentEvent ev)
    {
        try
        {
            // Resolve the id and token once per URL.
            if (_identifier == null || url != _url)
            {
                if (await _discord.GetWebhook(url) is not { } data)
                    return;

                if (url != _url)
                    return;

                _identifier = data.ToIdentifier();
            }

            var payload = new WebhookPayload
            {
                Username = Truncate(ev.Author, MaxUsernameLength),
                Content = Truncate(ev.Message, MaxContentLength),
            };

            await _discord.CreateMessage(_identifier.Value, payload);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to send dead chat to Discord: {e}");
        }
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }
}
