﻿using Kaiyuanshe.OpenHackathon.Server.Auth;
using Kaiyuanshe.OpenHackathon.Server.Helpers;
using Kaiyuanshe.OpenHackathon.Server.Models;
using Kaiyuanshe.OpenHackathon.Server.Storage.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;


namespace Kaiyuanshe.OpenHackathon.Server.Biz
{
    public interface IHackathonManagement
    {
        #region Hackathon
        /// <summary>
        /// Create a new hackathon
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<HackathonEntity> CreateHackathonAsync(Hackathon request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update hackathon from request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<HackathonEntity> UpdateHackathonAsync(Hackathon request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Change the status of a hackathon.
        /// </summary>
        /// <param name="hackathonEntity"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<HackathonEntity> UpdateHackathonStatusAsync(HackathonEntity hackathonEntity, HackathonStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get Hackathon By name. Return null if not found.
        /// </summary>
        /// <returns></returns>
        Task<HackathonEntity> GetHackathonEntityByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search hackathon
        /// </summary>
        /// <param name="options"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<HackathonEntity>> SearchHackathonAsync(HackathonSearchOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get roles of a user on a specified hackathon
        /// </summary>
        /// <param name="hackathonName"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        Task<HackathonRoles> GetHackathonRolesAsync(string hackathonName, ClaimsPrincipal user, CancellationToken cancellationToken = default);

        #endregion

        #region Admin
        /// <summary>
        /// List all Administrators of a Hackathon. PlatformAdministrator is not included.
        /// </summary>
        /// <param name="name">name of Hackathon</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<HackathonAdminEntity>> ListHackathonAdminAsync(string name, CancellationToken cancellationToken = default);
        #endregion

        #region Enrollment
        /// <summary>
        /// Register a hackathon event as contestant
        /// </summary>
        Task<EnrollmentEntity> EnrollAsync(HackathonEntity hackathon, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update status of enrollment.
        /// </summary>
        Task<EnrollmentEntity> UpdateEnrollmentStatusAsync(EnrollmentEntity participant, EnrollmentStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get an enrollment.
        /// </summary>
        Task<EnrollmentEntity> GetEnrollmentAsync(string hackathonName, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// List paged enrollments of hackathon
        /// </summary>
        /// <param name="hackathonName">name of hackathon</param>
        /// <param name="options">options for query</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TableQuerySegment<EnrollmentEntity>> ListPaginatedEnrollmentsAsync(string hackathonName, EnrollmentQueryOptions options, CancellationToken cancellationToken = default);
        #endregion
    }

    /// <inheritdoc cref="IHackathonManagement"/>
    public class HackathonManagement : ManagementClientBase, IHackathonManagement
    {
        private readonly ILogger Logger;

        public HackathonManagement(ILogger<HackathonManagement> logger)
        {
            Logger = logger;
        }

        #region Hackahton
        public async Task<HackathonEntity> CreateHackathonAsync(Hackathon request, CancellationToken cancellationToken = default)
        {
            #region Insert HackathonEntity
            var entity = new HackathonEntity
            {
                PartitionKey = request.name,
                RowKey = string.Empty,
                AutoApprove = request.autoApprove.HasValue ? request.autoApprove.Value : false,
                Ribbon = request.ribbon,
                Status = HackathonStatus.planning,
                Summary = request.summary,
                Tags = request.tags,
                MaxEnrollment = request.maxEnrollment.HasValue ? request.maxEnrollment.Value : 0,
                Banners = request.banners,
                CreatedAt = DateTime.UtcNow,
                CreatorId = request.creatorId,
                Detail = request.detail,
                DisplayName = request.displayName,
                EventStartedAt = request.eventStartedAt,
                EventEndedAt = request.eventEndedAt,
                EnrollmentStartedAt = request.enrollmentStartedAt,
                EnrollmentEndedAt = request.enrollmentEndedAt,
                JudgeStartedAt = request.judgeStartedAt,
                JudgeEndedAt = request.judgeEndedAt,
                Location = request.location,
            };
            await StorageContext.HackathonTable.InsertAsync(entity, cancellationToken);
            #endregion

            #region Add creator as Admin
            EnrollmentEntity participant = new EnrollmentEntity
            {
                PartitionKey = request.name,
                RowKey = request.creatorId,
                CreatedAt = DateTime.UtcNow,
            };
            await StorageContext.EnrollmentTable.InsertAsync(participant, cancellationToken);
            #endregion

            return entity;
        }

        public async Task<HackathonEntity> UpdateHackathonStatusAsync(HackathonEntity hackathonEntity, HackathonStatus status, CancellationToken cancellationToken = default)
        {
            if (hackathonEntity == null || hackathonEntity.Status == status)
                return hackathonEntity;

            hackathonEntity.Status = status;
            await StorageContext.HackathonTable.MergeAsync(hackathonEntity, cancellationToken);
            return hackathonEntity;
        }

        public async Task<HackathonEntity> GetHackathonEntityByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var entity = await StorageContext.HackathonTable.RetrieveAsync(name, string.Empty, cancellationToken);
            return entity;
        }

        public async Task<HackathonEntity> UpdateHackathonAsync(Hackathon request, CancellationToken cancellationToken = default)
        {
            await StorageContext.HackathonTable.RetrieveAndMergeAsync(request.name, string.Empty, (entity) =>
            {
                entity.Ribbon = request.ribbon ?? entity.Ribbon;
                entity.Summary = request.summary ?? entity.Summary;
                entity.Detail = request.detail ?? entity.Detail;
                entity.Location = request.location ?? entity.Location;
                entity.Banners = request.banners ?? entity.Banners;
                entity.DisplayName = request.displayName ?? entity.DisplayName;
                if (request.maxEnrollment.HasValue)
                    entity.MaxEnrollment = request.maxEnrollment.Value;
                if (request.autoApprove.HasValue)
                    entity.AutoApprove = request.autoApprove.Value;
                entity.Tags = request.tags ?? entity.Tags;
                if (request.eventStartedAt.HasValue)
                    entity.EventStartedAt = request.eventStartedAt.Value;
                if (request.eventEndedAt.HasValue)
                    entity.EventEndedAt = request.eventEndedAt.Value;
                if (request.enrollmentStartedAt.HasValue)
                    entity.EnrollmentStartedAt = request.enrollmentStartedAt.Value;
                if (request.enrollmentEndedAt.HasValue)
                    entity.EnrollmentEndedAt = request.enrollmentEndedAt.Value;
                if (request.judgeStartedAt.HasValue)
                    entity.JudgeStartedAt = request.judgeStartedAt.Value;
                if (request.judgeEndedAt.HasValue)
                    entity.JudgeEndedAt = request.judgeEndedAt.Value;
            }, cancellationToken);
            return await StorageContext.HackathonTable.RetrieveAsync(request.name, string.Empty, cancellationToken);
        }

        public async Task<IEnumerable<HackathonEntity>> SearchHackathonAsync(HackathonSearchOptions options, CancellationToken cancellationToken = default)
        {
            var entities = new List<HackathonEntity>();
            var filter = TableQuery.GenerateFilterConditionForInt(nameof(HackathonEntity.Status), QueryComparisons.Equal, (int)HackathonStatus.online);
            TableQuery<HackathonEntity> query = new TableQuery<HackathonEntity>().Where(filter);

            await StorageContext.HackathonTable.ExecuteQuerySegmentedAsync(query, (segment) =>
            {
                entities.AddRange(segment);
            }, cancellationToken);

            return entities;
        }

        public async Task<HackathonRoles> GetHackathonRolesAsync(string hackathonName, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            string userId = ClaimsHelper.GetUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                // no roles for anonymous user
                return null;
            }

            bool isAdmin = false, isEnrolled = false, isJudge = false;
            // admin
            if (ClaimsHelper.IsPlatformAdministrator(user))
            {
                isAdmin = true;
            }
            else
            {
                var admins = await ListHackathonAdminAsync(hackathonName, cancellationToken);
                isAdmin = admins.Any(a => a.UserId == userId);
            }

            // enrollment
            var enrollment = await GetEnrollmentAsync(hackathonName, userId, cancellationToken);
            isEnrolled = (enrollment != null && enrollment.Status == EnrollmentStatus.approved);

            // todo judge

            return new HackathonRoles
            {
                isAdmin = isAdmin,
                isEnrolled = isEnrolled,
                isJudge = isJudge
            };
        }
        #endregion

        #region Enrollment
        public virtual async Task<EnrollmentEntity> GetEnrollmentAsync(string hackathonName, string userId, CancellationToken cancellationToken = default)
        {
            if (hackathonName == null || userId == null)
                return null;
            return await StorageContext.EnrollmentTable.RetrieveAsync(hackathonName.ToLower(), userId.ToLower(), cancellationToken);
        }

        public async Task<EnrollmentEntity> EnrollAsync(HackathonEntity hackathon, string userId, CancellationToken cancellationToken)
        {
            string hackathonName = hackathon.Name;
            var entity = await StorageContext.EnrollmentTable.RetrieveAsync(hackathonName, userId, cancellationToken);

            if (entity != null)
            {
                Logger.TraceInformation($"Enroll skipped, user with id {userId} alreday enrolled in hackathon {hackathonName}");
                return entity;
            }
            else
            {
                entity = new EnrollmentEntity
                {
                    PartitionKey = hackathonName,
                    RowKey = userId,
                    Status = EnrollmentStatus.pendingApproval,
                    CreatedAt = DateTime.UtcNow,
                };
                if (hackathon.AutoApprove)
                {
                    entity.Status = EnrollmentStatus.approved;
                }
                await StorageContext.EnrollmentTable.InsertAsync(entity, cancellationToken);
            }
            Logger.TraceInformation($"user {userId} enrolled in hackathon {hackathon}, status: {entity.Status.ToString()}");

            return entity;
        }

        public async Task<EnrollmentEntity> UpdateEnrollmentStatusAsync(EnrollmentEntity participant, EnrollmentStatus status, CancellationToken cancellationToken = default)
        {
            if (participant == null)
                return participant;

            participant.Status = status;
            await StorageContext.EnrollmentTable.MergeAsync(participant, cancellationToken);
            Logger.TraceInformation($"Pariticipant {participant.HackathonName}/{participant.UserId} stastus updated to: {status} ");
            return participant;
        }

        public async Task<TableQuerySegment<EnrollmentEntity>> ListPaginatedEnrollmentsAsync(string hackathonName, EnrollmentQueryOptions options, CancellationToken cancellationToken = default)
        {
            var filter = TableQuery.GenerateFilterCondition(
                           nameof(EnrollmentEntity.PartitionKey),
                           QueryComparisons.Equal,
                           hackathonName);

            if (options != null && options.Status.HasValue)
            {
                var statusFilter = TableQuery.GenerateFilterConditionForInt(
                    nameof(EnrollmentEntity.Status),
                    QueryComparisons.Equal,
                    (int)options.Status.Value);
                filter = TableQueryHelper.And(filter, statusFilter);
            }

            int top = 100;
            if (options != null && options.Top.HasValue && options.Top.Value > 0)
            {
                top = options.Top.Value;
            }
            TableQuery<EnrollmentEntity> query = new TableQuery<EnrollmentEntity>()
                .Where(filter)
                .Take(top);

            TableContinuationToken continuationToken = options?.TableContinuationToken;
            return await StorageContext.EnrollmentTable.ExecuteQuerySegmentedAsync(query, continuationToken, cancellationToken);
        }
        #endregion

        #region Admin

        public virtual async Task<IEnumerable<HackathonAdminEntity>> ListHackathonAdminAsync(string name, CancellationToken cancellationToken = default)
        {
            string cacheKey = CacheKey.Get(CacheKey.Section.HackathonAdmin, name);
            return await CacheHelper.GetOrAddAsync(cacheKey,
                async () =>
                {
                    return await StorageContext.HackathonAdminTable.ListByHackathonAsync(name, cancellationToken);
                },
                CacheHelper.ExpireIn10M);
        }
        #endregion
    }

    public class HackathonSearchOptions
    {

    }


}
