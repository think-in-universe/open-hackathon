﻿using Kaiyuanshe.OpenHackathon.Server.Auth;
using Kaiyuanshe.OpenHackathon.Server.Biz;
using Kaiyuanshe.OpenHackathon.Server.Models;
using Kaiyuanshe.OpenHackathon.Server.ResponseBuilder;
using Kaiyuanshe.OpenHackathon.Server.Storage.Entities;
using Kaiyuanshe.OpenHackathon.Server.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace Kaiyuanshe.OpenHackathon.Server.Controllers
{
    public class AwardController : HackathonControllerBase
    {
        public IResponseBuilder ResponseBuilder { get; set; }

        public IHackathonManagement HackathonManagement { get; set; }

        public IAwardManagement AwardManagement { get; set; }

        #region CreateAward
        /// <summary>
        /// Create a new award
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="hackathonName" example="foo">Name of hackathon. Case-insensitive.
        /// Must contain only letters and/or numbers, length between 1 and 100</param>
        /// <returns>The award</returns>
        /// <response code="200">Success. The response describes a award.</response>
        [HttpPut]
        [ProducesResponseType(typeof(Award), StatusCodes.Status200OK)]
        [SwaggerErrorResponse(400, 404, 412)]
        [Route("hackathon/{hackathonName}/award")]
        [Authorize(Policy = AuthConstant.PolicyForSwagger.HackathonAdministrator)]
        public async Task<object> CreateAward(
            [FromRoute, Required, RegularExpression(ModelConstants.HackathonNamePattern)] string hackathonName,
            [FromBody] Award parameter,
            CancellationToken cancellationToken)
        {
            // validate hackathon
            var hackathon = await HackathonManagement.GetHackathonEntityByNameAsync(hackathonName.ToLower(), cancellationToken);
            var options = new ValidateHackathonOptions
            {
                HackAdminRequird = true,
                HackathonName = hackathonName,
            };
            if (await ValidateHackathon(hackathon, options, cancellationToken) == false)
            {
                return options.ValidateResult;
            }

            // create award
            parameter.hackathonName = hackathonName.ToLower();
            var awardEntity = await AwardManagement.CreateAwardAsync(hackathonName.ToLower(), parameter, cancellationToken);
            return Ok(ResponseBuilder.BuildAward(awardEntity));
        }
        #endregion

        #region GetAward
        /// <summary>
        /// Query an award
        /// </summary>
        /// <param name="hackathonName" example="foo">Name of hackathon. Case-insensitive.
        /// Must contain only letters and/or numbers, length between 1 and 100</param>
        /// <param name="awardId" example="c877c675-4c97-4deb-9e48-97d079fa4b72">unique Guid of the team. Auto-generated on server side.</param>
        /// <returns>The award</returns>
        /// <response code="200">Success. The response describes a award.</response>
        [HttpGet]
        [ProducesResponseType(typeof(Award), StatusCodes.Status200OK)]
        [SwaggerErrorResponse(400, 404)]
        [Route("hackathon/{hackathonName}/award/{awardId}")]
        public async Task<object> GetAward(
            [FromRoute, Required, RegularExpression(ModelConstants.HackathonNamePattern)] string hackathonName,
            [FromRoute, Required, StringLength(36, MinimumLength = 36)] string awardId,
            CancellationToken cancellationToken)
        {
            // validate hackathon
            var hackathon = await HackathonManagement.GetHackathonEntityByNameAsync(hackathonName.ToLower(), cancellationToken);
            var options = new ValidateHackathonOptions
            {
                HackathonName = hackathonName,
            };
            if (await ValidateHackathon(hackathon, options, cancellationToken) == false)
            {
                return options.ValidateResult;
            }

            // get award
            var award = await AwardManagement.GetAwardByIdAsync(hackathonName.ToLower(), awardId, cancellationToken);
            var awardOptions = new ValidateAwardOptions { };
            if (ValidateAward(award, awardOptions) == false)
            {
                return awardOptions.ValidateResult;
            }
            return Ok(ResponseBuilder.BuildAward(award));
        }
        #endregion

        #region UpdateAward
        /// <summary>
        /// Update an award
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="hackathonName" example="foo">Name of hackathon. Case-insensitive.
        /// Must contain only letters and/or numbers, length between 1 and 100</param>
        /// <param name="awardId" example="c877c675-4c97-4deb-9e48-97d079fa4b72">unique Guid of the team. Auto-generated on server side.</param>
        /// <returns>The award</returns>
        /// <response code="200">Success. The response describes a award.</response>
        [HttpPatch]
        [ProducesResponseType(typeof(Award), StatusCodes.Status200OK)]
        [SwaggerErrorResponse(400, 403, 404)]
        [Route("hackathon/{hackathonName}/award/{awardId}")]
        [Authorize(Policy = AuthConstant.PolicyForSwagger.HackathonAdministrator)]
        public async Task<object> UpdateAward(
            [FromRoute, Required, RegularExpression(ModelConstants.HackathonNamePattern)] string hackathonName,
            [FromRoute, Required, StringLength(36, MinimumLength = 36)] string awardId,
            [FromBody] Award parameter,
            CancellationToken cancellationToken)
        {
            // validate hackathon
            var hackathon = await HackathonManagement.GetHackathonEntityByNameAsync(hackathonName.ToLower(), cancellationToken);
            var options = new ValidateHackathonOptions
            {
                HackAdminRequird = true,
                HackathonName = hackathonName,
            };
            if (await ValidateHackathon(hackathon, options, cancellationToken) == false)
            {
                return options.ValidateResult;
            }

            // validate award
            var award = await AwardManagement.GetAwardByIdAsync(hackathonName.ToLower(), awardId, cancellationToken);
            var awardOptions = new ValidateAwardOptions { };
            if (ValidateAward(award, awardOptions) == false)
            {
                return awardOptions.ValidateResult;
            }

            // update award
            var updated = await AwardManagement.UpdateAwardAsync(award, parameter, cancellationToken);
            return Ok(ResponseBuilder.BuildAward(updated));
        }
        #endregion

        #region ListAwards
        /// <summary>
        /// List paginated awards of a hackathon.
        /// </summary>
        /// <param name="hackathonName" example="foo">Name of hackathon. Case-insensitive.
        /// Must contain only letters and/or numbers, length between 1 and 100</param>
        /// <returns>the response contains a list of awards and a nextLink if there are more results.</returns>
        /// <response code="200">Success.</response>
        [HttpGet]
        [ProducesResponseType(typeof(AwardList), StatusCodes.Status200OK)]
        [SwaggerErrorResponse(400, 404)]
        [Route("hackathon/{hackathonName}/awards")]
        public async Task<object> ListAwards(
            [FromRoute, Required, RegularExpression(ModelConstants.HackathonNamePattern)] string hackathonName,
            [FromQuery] Pagination pagination,
            CancellationToken cancellationToken)
        {
            // validate hackathon
            var hackathon = await HackathonManagement.GetHackathonEntityByNameAsync(hackathonName.ToLower());
            var options = new ValidateHackathonOptions
            {
                HackathonName = hackathonName,
            };
            if (await ValidateHackathon(hackathon, options) == false)
            {
                return options.ValidateResult;
            }

            // query
            var awardQueryOptions = new AwardQueryOptions
            {
                TableContinuationToken = pagination.ToContinuationToken(),
                Top = pagination.top
            };
            var segment = await AwardManagement.ListPaginatedAwardsAsync(hackathonName.ToLower(), awardQueryOptions, cancellationToken);
            var routeValues = new RouteValueDictionary();
            if (pagination.top.HasValue)
            {
                routeValues.Add(nameof(pagination.top), pagination.top.Value);
            }
            var nextLink = BuildNextLinkUrl(routeValues, segment.ContinuationToken);
            return Ok(ResponseBuilder.BuildResourceList<AwardEntity, Award, AwardList>(
                    segment,
                    ResponseBuilder.BuildAward,
                    nextLink));
        }
        #endregion

        #region DeleteAward
        /// <summary>
        /// Delete an award
        /// </summary>
        /// <param name="hackathonName" example="foo">Name of hackathon. Case-insensitive.
        /// Must contain only letters and/or numbers, length between 1 and 100</param>
        /// <param name="awardId" example="c877c675-4c97-4deb-9e48-97d079fa4b72">unique Guid of the team. Auto-generated on server side.</param>
        /// <response code="204">Success. The response indicates that a award is deleted.</response>
        [HttpDelete]
        [SwaggerErrorResponse(400, 404)]
        [Route("hackathon/{hackathonName}/award/{awardId}")]
        public async Task<object> DeleteAward(
            [FromRoute, Required, RegularExpression(ModelConstants.HackathonNamePattern)] string hackathonName,
            [FromRoute, Required, StringLength(36, MinimumLength = 36)] string awardId,
            CancellationToken cancellationToken)
        {
            // validate hackathon
            var hackathon = await HackathonManagement.GetHackathonEntityByNameAsync(hackathonName.ToLower(), cancellationToken);
            var options = new ValidateHackathonOptions
            {
                HackathonName = hackathonName,
            };
            if (await ValidateHackathon(hackathon, options, cancellationToken) == false)
            {
                return options.ValidateResult;
            }

            // get award
            var award = await AwardManagement.GetAwardByIdAsync(hackathonName.ToLower(), awardId, cancellationToken);
            if(award == null)
            {
                return NoContent();
            }

            // delete award
            await AwardManagement.DeleteAwardAsync(award, cancellationToken);
            return NoContent();
        }
        #endregion
    }
}
