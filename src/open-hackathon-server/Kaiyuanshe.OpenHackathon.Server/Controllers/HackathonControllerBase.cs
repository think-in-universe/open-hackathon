﻿using Kaiyuanshe.OpenHackathon.Server.Auth;
using Kaiyuanshe.OpenHackathon.Server.Models;
using Kaiyuanshe.OpenHackathon.Server.Storage.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kaiyuanshe.OpenHackathon.Server.Controllers
{
    [ApiController]
    [Consumes(MediaTypeNames.Application.Json)]
    [Produces(MediaTypeNames.Application.Json)]
    [Route("v2")]
    public abstract class HackathonControllerBase : ControllerBase
    {
        public IAuthorizationService AuthorizationService { get; set; }

        /// <summary>
        /// Id of current User. Return string.Empty if token is not required or invalid.
        /// </summary>
        protected string CurrentUserId
        {
            get
            {
                return ClaimsHelper.GetUserId(User);
            }
        }

        /// <summary>
        /// Get nextLink url for paginated results
        /// </summary>
        /// <param name="routeValues">values to generate url. Values of current url are implicitly used. 
        /// Add extra key/value pairs or modifications to routeValues. Values not used in route will be appended as QueryString.</param>
        /// <returns></returns>
        protected string BuildNextLinkUrl(RouteValueDictionary routeValues, TableContinuationToken continuationToken)
        {
            if (continuationToken == null)
                return null;

            if (routeValues == null)
            {
                routeValues = new RouteValueDictionary();
            }
            routeValues.Add(nameof(Pagination.np), continuationToken.NextPartitionKey);
            routeValues.Add(nameof(Pagination.nr), continuationToken.NextRowKey);

            if (EnvHelper.IsRunningInTests())
            {
                // Unit Test
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var key in routeValues.Keys)
                {
                    stringBuilder.Append($"&{key}={routeValues[key]}");
                }
                return stringBuilder.ToString();
            }

            return Url.Action(
               ControllerContext.ActionDescriptor.ActionName,
               ControllerContext.ActionDescriptor.ControllerName,
               routeValues,
               Request.Scheme,
               Request.Host.Value);
        }

        #region ObjectResult with ProblemDetails
        protected ObjectResult BadRequest(string detail, string instance = null)
        {
            return Problem(
                statusCode: 400,
                detail: detail,
                instance: instance);
        }

        protected ObjectResult Unauthorized(string detail, string instance = null)
        {
            return Problem(
                statusCode: 401,
                detail: detail,
                instance: instance);
        }

        protected ObjectResult Forbidden(string detail, string instance = null)
        {
            return Problem(
                statusCode: 403,
                detail: detail,
                instance: instance);
        }

        protected ObjectResult NotFound(string detail, string instance = null)
        {
            return Problem(
                statusCode: 404,
                detail: detail,
                instance: instance);
        }

        protected ObjectResult Conflict(string detail, string instance = null)
        {
            return Problem(
                statusCode: 409,
                detail: detail,
                instance: instance);
        }

        protected ObjectResult PreconditionFailed(string detail, string instance = null)
        {
            return Problem(
                statusCode: 412,
                detail: detail,
                instance: instance
            );
        }
        #endregion

        #region Frequently used validations

        public class ControllerValiationOptions
        {
            /// <summary>
            /// null if validate successfully. Otherwise a response which desribes the failure
            /// and can be returned to client.
            /// </summary>
            public object ValidateResult { get; set; }

            /// <summary>
            /// optional hackathon name for error message 
            /// </summary>
            public string HackathonName { get; set; } = string.Empty;

            /// <summary>
            /// optional Team Id for error message 
            /// </summary>
            public string TeamId { get; set; } = string.Empty;

            /// <summary>
            /// optional {userId} in Route for error message
            /// </summary>
            public string UserId { get; set; } = string.Empty;

        }

        public class ValidateHackathonOptions : ControllerValiationOptions
        {
            public bool EnrollmentOpenRequired { get; set; }
            public bool HackathonOpenRequired { get; set; }
            public bool HackAdminRequird { get; set; }
            public bool OnlineRequired { get; set; }
            public bool NotDeletedRequired { get; set; }
        }

        public class ValidateEnrollmentOptions : ControllerValiationOptions
        {
            public bool ApprovedRequired { get; set; }
        }

        public class ValidateTeamOptions : ControllerValiationOptions
        {
            public bool TeamAdminRequired { get; set; }
        }

        public class ValidateTeamMemberOptions : ControllerValiationOptions
        {
            public bool ApprovedMemberRequired { get; set; }
        }

        public class ValidateAwardOptions : ControllerValiationOptions
        {
        }

        #region ValidateHackathon
        protected async Task<bool> ValidateHackathon(HackathonEntity hackathon,
            ValidateHackathonOptions options,
            CancellationToken cancellationToken = default)
        {
            options.ValidateResult = null; // make sure it's not set by caller

            if (hackathon == null)
            {
                options.ValidateResult = NotFound(string.Format(Resources.Hackathon_NotFound, options.HackathonName));
                return false;
            }

            if (options.OnlineRequired && hackathon.Status != HackathonStatus.online)
            {
                options.ValidateResult = NotFound(string.Format(Resources.Hackathon_NotFound, options.HackathonName));
                return false;
            }

            if (options.NotDeletedRequired && hackathon.Status == HackathonStatus.offline)
            {
                options.ValidateResult = PreconditionFailed(string.Format(Resources.Hackathon_Deleted, options.HackathonName));
                return false;
            }

            if (options.HackathonOpenRequired)
            {
                if (hackathon.EventStartedAt.HasValue && DateTime.UtcNow < hackathon.EventStartedAt.Value)
                {
                    // event not started
                    options.ValidateResult = PreconditionFailed(Resources.Hackathon_NotStarted);
                    return false;
                }

                if (hackathon.EventEndedAt.HasValue && DateTime.UtcNow > hackathon.EventEndedAt.Value)
                {
                    // event ended
                    options.ValidateResult = PreconditionFailed(string.Format(Resources.Hackathon_Ended, hackathon.EventEndedAt.Value));
                    return false;
                }
            }

            if (options.EnrollmentOpenRequired)
            {
                if (hackathon.EnrollmentStartedAt.HasValue && DateTime.UtcNow < hackathon.EnrollmentStartedAt.Value)
                {
                    // enrollment not started
                    options.ValidateResult = PreconditionFailed(string.Format(Resources.Enrollment_NotStarted, hackathon.EnrollmentStartedAt.Value));
                    return false;
                }

                if (hackathon.EnrollmentEndedAt.HasValue && DateTime.UtcNow > hackathon.EnrollmentEndedAt.Value)
                {
                    // enrollment ended
                    options.ValidateResult = PreconditionFailed(string.Format(Resources.Enrollment_Ended, hackathon.EnrollmentEndedAt.Value));
                    return false;
                }
            }

            if (options.HackAdminRequird)
            {
                var authorizationResult = await AuthorizationService.AuthorizeAsync(User, hackathon, AuthConstant.Policy.HackathonAdministrator);
                if (!authorizationResult.Succeeded)
                {
                    options.ValidateResult = Forbidden(Resources.Hackathon_NotAdmin);
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region ValidateEnrollment
        protected bool ValidateEnrollment(EnrollmentEntity enrollment, ValidateEnrollmentOptions options)
        {
            if (enrollment == null)
            {
                options.ValidateResult = NotFound(string.Format(Resources.Enrollment_NotFound, options.UserId ?? CurrentUserId, options.HackathonName));
                return false;
            }

            if (options.ApprovedRequired && enrollment.Status != EnrollmentStatus.approved)
            {
                options.ValidateResult = PreconditionFailed(Resources.Enrollment_NotApproved);
                return false;
            }

            return true;
        }
        #endregion

        #region ValidateTeam
        protected async Task<bool> ValidateTeam(TeamEntity team,
            ValidateTeamOptions options,
            CancellationToken cancellationToken = default)
        {
            if (team == null)
            {
                options.ValidateResult = NotFound(Resources.Team_NotFound);
                return false;
            }

            if (options.TeamAdminRequired)
            {
                var authorizationResult = await AuthorizationService.AuthorizeAsync(User, team, AuthConstant.Policy.TeamAdministrator);
                if (!authorizationResult.Succeeded)
                {
                    options.ValidateResult = Forbidden(Resources.Team_NotAdmin);
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region ValidateTeamMember
        protected async Task<bool> ValidateTeamMember(TeamEntity team, TeamMemberEntity teamMember,
            ValidateTeamMemberOptions options,
            CancellationToken cancellationToken)
        {
            if (team == null)
            {
                options.ValidateResult = NotFound(Resources.Team_NotFound);
                return false;
            }

            if (teamMember == null)
            {
                options.ValidateResult = NotFound(string.Format(Resources.TeamMember_NotFound, options.UserId ?? CurrentUserId, options.TeamId));
                return false;
            }

            if (options.ApprovedMemberRequired)
            {
                var authorizationResult = await AuthorizationService.AuthorizeAsync(User, team, AuthConstant.Policy.TeamMember);
                if (!authorizationResult.Succeeded)
                {
                    options.ValidateResult = Forbidden(string.Format(Resources.TeamMember_AccessDenied, CurrentUserId));
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region ValidateAward
        protected bool ValidateAward(AwardEntity award, ValidateAwardOptions options)
        {
            if (award == null)
            {
                options.ValidateResult = NotFound(Resources.Award_NotFound);
                return false;
            }

            return true;
        }
        #endregion

        #endregion
    }
}
