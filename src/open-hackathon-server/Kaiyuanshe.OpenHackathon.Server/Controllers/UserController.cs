﻿using Kaiyuanshe.OpenHackathon.Server.Biz;
using Kaiyuanshe.OpenHackathon.Server.Models;
using Kaiyuanshe.OpenHackathon.Server.ResponseBuilder;
using Kaiyuanshe.OpenHackathon.Server.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace Kaiyuanshe.OpenHackathon.Server.Controllers
{
    public class UserController : HackathonControllerBase
    {
        public IUserManagement UserManagement { get; set; }

        public IResponseBuilder ResponseBuilder { get; set; }

        #region Authing
        /// <summary>
        /// Post the data from Authing after completing the Login process. Open hackathon API
        /// relies on the data for user profile and the token.
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost]
        [SwaggerErrorResponse(400)]
        [Route("login")]
        public async Task<object> Authing([FromBody] UserInfo parameter,
            CancellationToken cancellationToken)
        {
            var tokenStatus = await UserManagement.ValidateTokenRemotelyAsync(parameter.UserPoolId, parameter.Token, cancellationToken);
            if (!tokenStatus.Status.GetValueOrDefault(false))
            {
                // token invalid
                return BadRequest(string.Format(
                    Resources.Auth_Token_ValidateRemoteFailed,
                    tokenStatus.Code.GetValueOrDefault(0),
                    tokenStatus.Message));
            }


            await UserManagement.AuthingAsync(parameter, cancellationToken);
            return Ok(parameter);
        }
        #endregion

        #region GetUserById
        /// <summary>
        /// Get user info by user id. The info is synced from Authing during login.
        /// </summary>
        /// <param name="userId" example="1">unique id of the user</param>
        /// <param name="cancellationToken"></param>
        /// <returns>the user</returns>
        /// <response code="200">Success. The response describes a user.</response>
        [HttpGet]
        [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
        [SwaggerErrorResponse(404)]
        [Route("user/{userId}")]
        public async Task<object> GetUserById([FromRoute, Required] string userId,
            CancellationToken cancellationToken)
        {
            var userInfo = await UserManagement.GetUserByIdAsync(userId, cancellationToken);
            return Ok(userInfo);
        }
        #endregion
    }
}
