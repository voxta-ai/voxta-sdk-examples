using System.Net.Http.Headers;
using System.Net.Http.Json;
using Voxta.Client;
using Voxta.Model.ApiMessages.Responses;
using Voxta.Model.Serialization;

namespace Voxta.ConsoleChat.Api;

public class ChatsApiClient(HttpClient httpClient, IVoxtaTokenProvider tokenProvider)
{
    public async Task<IReadOnlyList<ChatItemResponse>> GetChatsAsync(Guid characterId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/chats?characterId={characterId}");
        var token = tokenProvider.GetToken();
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatsResponse>(VoxtaJsonSerializer.SerializeOptions, cancellationToken);
        return result?.Chats ?? [];
    }
}
