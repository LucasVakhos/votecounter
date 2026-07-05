using Microsoft.AspNetCore.Mvc;
using Rhymers.Core.Models;
using Rhymers.Web.Services;

namespace Rhymers.Web.Controllers;

/// <summary>
/// API endpoints для управления дружбой между пользователями (STAGE29)
/// </summary>
[ApiController]
[Route("api/friendship")]
public class FriendshipController : ControllerBase
{
    private readonly FriendshipWebService _friendshipService;

    public FriendshipController(FriendshipWebService friendshipService)
    {
        _friendshipService = friendshipService;
    }

    /// <summary>
    /// Получить список друзей текущего пользователя
    /// </summary>
    [HttpGet("friends")]
    public async Task<ActionResult<List<(User Friend, UserFriendship Friendship)>>> GetFriends([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        var friends = await _friendshipService.GetFriendsWithDetailsAsync(userId);
        return Ok(friends);
    }

    /// <summary>
    /// Отправить приглашение в друзья
    /// </summary>
    [HttpPost("invite")]
    public async Task<ActionResult<FriendshipInvitation>> SendInvitation([FromBody] SendInvitationRequest request)
    {
        if (string.IsNullOrEmpty(request.FromUserId) || string.IsNullOrEmpty(request.ToUserId))
            return BadRequest("FromUserId и ToUserId обязательны");

        try
        {
            var invitation = await _friendshipService.SendInvitationAsync(request.FromUserId, request.ToUserId, request.Message);
            return CreatedAtAction(nameof(GetIncomingInvitations), new { userId = request.ToUserId }, invitation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Получить входящие приглашения в друзья
    /// </summary>
    [HttpGet("invitations/incoming")]
    public async Task<ActionResult<List<(User Sender, FriendshipInvitation Invitation)>>> GetIncomingInvitations([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        var invitations = await _friendshipService.GetIncomingInvitationsWithDetailsAsync(userId);
        return Ok(invitations);
    }

    /// <summary>
    /// Получить исходящие приглашения в друзья
    /// </summary>
    [HttpGet("invitations/outgoing")]
    public async Task<ActionResult<List<(User Recipient, FriendshipInvitation Invitation)>>> GetOutgoingInvitations([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        var invitations = await _friendshipService.GetOutgoingInvitationsWithDetailsAsync(userId);
        return Ok(invitations);
    }

    /// <summary>
    /// Принять приглашение в друзья
    /// </summary>
    [HttpPost("invitations/{invitationId}/accept")]
    public async Task<ActionResult<UserFriendship>> AcceptInvitation(string invitationId, [FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        try
        {
            var friendship = await _friendshipService.AcceptInvitationAsync(invitationId, userId);
            return Ok(friendship);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Приглашение не найдено");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    /// <summary>
    /// Отклонить приглашение в друзья
    /// </summary>
    [HttpPost("invitations/{invitationId}/decline")]
    public async Task<IActionResult> DeclineInvitation(string invitationId, [FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        try
        {
            await _friendshipService.DeclineInvitationAsync(invitationId, userId);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Приглашение не найдено");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    /// <summary>
    /// Удалить из друзей
    /// </summary>
    [HttpDelete("friends/{friendId}")]
    public async Task<IActionResult> RemoveFriend(string friendId, [FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        try
        {
            await _friendshipService.RemoveFriendAsync(userId, friendId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Дружба не найдена");
        }
    }

    /// <summary>
    /// Заблокировать пользователя
    /// </summary>
    [HttpPost("block")]
    public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
    {
        if (string.IsNullOrEmpty(request.BlockingUserId) || string.IsNullOrEmpty(request.BlockedUserId))
            return BadRequest("BlockingUserId и BlockedUserId обязательны");

        try
        {
            await _friendshipService.BlockUserAsync(request.BlockingUserId, request.BlockedUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Проверить, являются ли пользователи друзьями
    /// </summary>
    [HttpGet("check")]
    public async Task<ActionResult<bool>> CheckFriendship([FromQuery] string userId1, [FromQuery] string userId2)
    {
        if (string.IsNullOrEmpty(userId1) || string.IsNullOrEmpty(userId2))
            return BadRequest("userId1 и userId2 обязательны");

        var areFriends = await _friendshipService.AreFriendsAsync(userId1, userId2);
        return Ok(areFriends);
    }

    /// <summary>
    /// Найти пользователя по имени
    /// </summary>
    [HttpGet("find")]
    public async Task<ActionResult<User>> FindUser([FromQuery] string username)
    {
        if (string.IsNullOrEmpty(username))
            return BadRequest("username обязателен");

        var user = await _friendshipService.FindUserAsync(username);
        if (user == null)
            return NotFound();

        return Ok(user);
    }
}

/// <summary>Запрос на отправку приглашения в друзья</summary>
public class SendInvitationRequest
{
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string? Message { get; set; }
}

/// <summary>Запрос на блокировку пользователя</summary>
public class BlockUserRequest
{
    public string BlockingUserId { get; set; } = string.Empty;
    public string BlockedUserId { get; set; } = string.Empty;
}
