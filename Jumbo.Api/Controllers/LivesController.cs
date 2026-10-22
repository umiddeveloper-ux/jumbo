using Jumbo.Api.Auth;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Jumbo.Api.Controllers;

/// <summary>The learner's lives and LP. Only the server changes them; the clients ask and show the answer.</summary>
[ApiController]
[Route("api/lives")]
[RequireAccessToken]
public class LivesController(ICurrentUserService currentUser, Services.LivesService lives) : ControllerBase
{
    /// <summary>The numbers now (the lives come back here when the block is over).</summary>
    [HttpGet]
    public async Task<LivesStateDto> Get()
    {
        Response.Headers.CacheControl = "no-store";
        return await lives.GetAsync((await currentUser.GetUserIdAsync())!.Value);
    }

    /// <summary>A wrong answer in a game or in the check of a book.</summary>
    [HttpPost("mistake")]
    public async Task<LivesStateDto> Mistake(MistakeRequest request) =>
        await lives.MistakeAsync((await currentUser.GetUserIdAsync())!.Value, request.LivesOnly);
}
