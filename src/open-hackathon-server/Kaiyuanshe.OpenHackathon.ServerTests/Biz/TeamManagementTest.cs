﻿using Kaiyuanshe.OpenHackathon.Server.Biz;
using Kaiyuanshe.OpenHackathon.Server.Cache;
using Kaiyuanshe.OpenHackathon.Server.Models;
using Kaiyuanshe.OpenHackathon.Server.Storage;
using Kaiyuanshe.OpenHackathon.Server.Storage.Entities;
using Kaiyuanshe.OpenHackathon.Server.Storage.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kaiyuanshe.OpenHackathon.ServerTests.Biz
{
    [TestFixture]
    public class TeamManagementTest
    {
        [TestCase(null, "uid")]
        [TestCase("", "uid")]
        [TestCase(" ", "uid")]
        [TestCase("foo", null)]
        [TestCase("foo", "")]
        [TestCase("foo", " ")]
        public async Task CreateTeamAsync_Null(string hackathonName, string userId)
        {
            var request = new Team { hackathonName = hackathonName, creatorId = userId };
            var cancellationToken = CancellationToken.None;
            var logger = new Mock<ILogger<TeamManagement>>();
            var teamManagement = new TeamManagement(logger.Object);
            Assert.IsNull(await teamManagement.CreateTeamAsync(request, cancellationToken));
            Mock.VerifyAll(logger);
        }

        [Test]
        public async Task CreateTeamAsync_Succeed()
        {
            var request = new Team
            {
                hackathonName = "hack",
                creatorId = "uid",
                description = "desc",
                autoApprove = false,
                displayName = "dp",
            };
            var cancellationToken = CancellationToken.None;
            TeamMemberEntity teamMember = null;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamTable = new Mock<ITeamTable>();
            teamTable.Setup(p => p.InsertAsync(It.IsAny<TeamEntity>(), cancellationToken));
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(p => p.InsertAsync(It.IsAny<TeamMemberEntity>(), cancellationToken))
                .Callback<TeamMemberEntity, CancellationToken>((t, c) => { teamMember = t; });
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamTable).Returns(teamTable.Object);
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);

            var teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.CreateTeamAsync(request, cancellationToken);

            Mock.VerifyAll(logger, teamTable, teamMemberTable, storageContext);
            Assert.IsNotNull(result);
            Assert.AreEqual(false, result.AutoApprove);
            Assert.AreEqual("uid", result.CreatorId);
            Assert.AreEqual("desc", result.Description);
            Assert.AreEqual("dp", result.DisplayName);
            Assert.AreEqual("hack", result.HackathonName);
            Assert.IsNotNull(result.Id);

            Assert.IsNotNull(teamMember);
            Assert.AreEqual("hack", teamMember.HackathonName);
            Assert.AreEqual(result.Id, teamMember.PartitionKey);
            Assert.AreEqual(TeamMemberRole.Admin, teamMember.Role);
            Assert.AreEqual(TeamMemberStatus.approved, teamMember.Status);
            Assert.AreEqual(result.Id, teamMember.TeamId);
            Assert.AreEqual("uid", teamMember.UserId);
            Assert.AreEqual(result.CreatedAt, teamMember.CreatedAt);
        }

        [Test]
        public async Task ListTeamMembersAsync()
        {
            var cancellationToken = CancellationToken.None;
            List<TeamMemberEntity> teamMembers = new List<TeamMemberEntity>
            {
                new TeamMemberEntity
                {
                    RowKey = "rk1",
                    Role = TeamMemberRole.Admin,
                    Status = TeamMemberStatus.approved
                },
                new TeamMemberEntity
                {
                    RowKey = "rk2",
                    Role = TeamMemberRole.Member,
                    Status = TeamMemberStatus.pendingApproval
                },
            };

            string query = null;
            TableContinuationToken continuationToken = null;
            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(p => p.ExecuteQuerySegmentedAsync(It.IsAny<TableQuery<TeamMemberEntity>>(), It.IsAny<TableContinuationToken>(), cancellationToken))
                .Callback<TableQuery<TeamMemberEntity>, TableContinuationToken, CancellationToken>((q, t, c) =>
                {
                    query = q.FilterString;
                    continuationToken = t;
                })
                .ReturnsAsync(MockHelper.CreateTableQuerySegment(teamMembers, null));
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);
            var cache = new DefaultCacheProvider(null);

            var teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
                Cache = cache,
            };
            var results = await teamManagement.ListTeamMembersAsync("tid", cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            Assert.AreEqual("PartitionKey eq 'tid'", query);
            Assert.IsNull(continuationToken);
            Assert.AreEqual(2, results.Count());
            Assert.AreEqual("rk1", results.First().UserId);
            Assert.AreEqual(TeamMemberRole.Admin, results.First().Role);
            Assert.AreEqual(TeamMemberStatus.approved, results.First().Status);
            Assert.AreEqual("rk2", results.Last().UserId);
            Assert.AreEqual(TeamMemberRole.Member, results.Last().Role);
            Assert.AreEqual(TeamMemberStatus.pendingApproval, results.Last().Status);
        }

        [Test]
        public async Task UpdateTeamAsync_Null()
        {
            var cancellationToken = CancellationToken.None;
            var logger = new Mock<ILogger<TeamManagement>>();
            var storageContext = new Mock<IStorageContext>();

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            Assert.IsNull(await teamManagement.UpdateTeamAsync(null, null, cancellationToken));
            Assert.IsNull(await teamManagement.UpdateTeamAsync(new Team(), null, cancellationToken));

            TeamEntity entity = new TeamEntity();
            Assert.AreEqual(entity, await teamManagement.UpdateTeamAsync(null, entity, cancellationToken));
            Mock.VerifyAll(logger, storageContext);
            logger.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
        }

        [Test]
        public async Task UpdateTeamAsync_Updated()
        {
            var request = new Team { description = "newdesc", autoApprove = true };
            var entity = new TeamEntity { Description = "desc", DisplayName = "dp", AutoApprove = false };

            var cancellationToken = CancellationToken.None;
            var logger = new Mock<ILogger<TeamManagement>>();
            var teamTable = new Mock<ITeamTable>();
            teamTable.Setup(t => t.MergeAsync(entity, cancellationToken));
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamTable).Returns(teamTable.Object);

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.UpdateTeamAsync(request, entity, cancellationToken);

            Mock.VerifyAll(logger, storageContext, teamTable);
            logger.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
            teamTable.VerifyNoOtherCalls();
            Assert.AreEqual("newdesc", result.Description);
            Assert.AreEqual("dp", result.DisplayName);
            Assert.AreEqual(true, result.AutoApprove);
        }

        [TestCase(null, null)]
        [TestCase(null, "")]
        [TestCase(null, " ")]
        [TestCase("", null)]
        [TestCase(" ", null)]
        public async Task GetTeamByIdAsync_Null(string hackName, string teamId)
        {
            var cancellationToken = CancellationToken.None;
            var logger = new Mock<ILogger<TeamManagement>>();
            TeamManagement teamManagement = new TeamManagement(logger.Object);
            var result = await teamManagement.GetTeamByIdAsync(hackName, teamId, cancellationToken);

            Mock.VerifyAll(logger);
            logger.VerifyNoOtherCalls();
            Assert.IsNull(result);
        }

        [Test]
        public async Task GetTeamByIdAsync_Succeeded()
        {
            string hackName = "Hack";
            string teamId = "tid";
            var cancellationToken = CancellationToken.None;
            TeamEntity teamEntity = new TeamEntity { Description = "desc" };

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamTable = new Mock<ITeamTable>();
            teamTable.Setup(t => t.RetrieveAsync("hack", "tid", cancellationToken))
                .ReturnsAsync(teamEntity);
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamTable).Returns(teamTable.Object);

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.GetTeamByIdAsync(hackName, teamId, cancellationToken);

            Mock.VerifyAll(logger, storageContext, teamTable);
            logger.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
            teamTable.VerifyNoOtherCalls();
            Assert.AreEqual("desc", result.Description);
        }

        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("", " ")]
        [TestCase(null, "")]
        [TestCase(null, " ")]
        public async Task GetTeamMember_Null(string teamId, string userId)
        {
            var cancellationToken = CancellationToken.None;
            var logger = new Mock<ILogger<TeamManagement>>();
            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
            };
            var result = await teamManagement.GetTeamMemberAsync(teamId, userId, cancellationToken);
            Assert.IsNull(result);
        }

        [Test]
        public async Task GetTeamMember_Suecceded()
        {
            string teamId = "tid";
            string userId = "uid";
            var cancellationToken = CancellationToken.None;
            TeamMemberEntity teamMember = new TeamMemberEntity { Description = "desc" };

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(t => t.RetrieveAsync("tid", "uid", cancellationToken))
                .ReturnsAsync(teamMember);
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.GetTeamMemberAsync(teamId, userId, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
            Assert.IsNotNull(result);
            Assert.AreEqual("desc", result.Description);
        }

        [Test]
        public async Task CreateTeamMemberAsync()
        {
            TeamMember request = new TeamMember { hackathonName = "hack", };
            CancellationToken cancellationToken = CancellationToken.None;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(t => t.InsertAsync(It.IsAny<TeamMemberEntity>(), cancellationToken));
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.CreateTeamMemberAsync(request, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
            Assert.AreEqual("hack", result.HackathonName);
            Assert.AreEqual(TeamMemberRole.Member, result.Role);
        }

        [Test]
        public async Task UpdateTeamMemberAsync()
        {
            TeamMember request = new TeamMember { description = "b" };
            CancellationToken cancellationToken = CancellationToken.None;
            TeamMemberEntity member = new TeamMemberEntity { Description = "a", Role = TeamMemberRole.Member, Status = TeamMemberStatus.pendingApproval };
            TeamMemberEntity captured = null;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(t => t.MergeAsync(It.IsAny<TeamMemberEntity>(), cancellationToken))
                .Callback<TeamMemberEntity, CancellationToken>((tm, c) => { captured = tm; });
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.UpdateTeamMemberAsync(member, request, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
            Assert.AreEqual("b", result.Description);
            Assert.AreEqual("b", captured.Description);
            Assert.AreEqual(TeamMemberRole.Member, result.Role);
            Assert.AreEqual(TeamMemberStatus.pendingApproval, result.Status);
        }

        [TestCase(TeamMemberStatus.approved)]
        [TestCase(TeamMemberStatus.pendingApproval)]
        public async Task UpdateTeamMemberStatusAsync_Skip(TeamMemberStatus status)
        {
            TeamMemberEntity teamMember = new TeamMemberEntity { Status = status };
            CancellationToken cancellationToken = CancellationToken.None;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            var storageContext = new Mock<IStorageContext>();

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.UpdateTeamMemberStatusAsync(teamMember, status, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
        }

        [TestCase(TeamMemberStatus.pendingApproval, TeamMemberStatus.approved)]
        [TestCase(TeamMemberStatus.approved, TeamMemberStatus.pendingApproval)]
        public async Task UpdateTeamMemberStatusAsync_Succeeded(TeamMemberStatus oldStatus, TeamMemberStatus newStatus)
        {
            TeamMemberEntity teamMember = new TeamMemberEntity { Status = oldStatus };
            CancellationToken cancellationToken = CancellationToken.None;
            TeamMemberEntity captured = null;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(t => t.MergeAsync(It.IsAny<TeamMemberEntity>(), cancellationToken))
                .Callback<TeamMemberEntity, CancellationToken>((tm, c) => { captured = tm; });
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);


            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.UpdateTeamMemberStatusAsync(teamMember, newStatus, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
            Assert.IsNull(result.Description);
            Assert.IsNull(captured.Description);
            Assert.AreEqual(newStatus, result.Status);
            Assert.AreEqual(newStatus, captured.Status);
        }

        [TestCase(TeamMemberRole.Member)]
        [TestCase(TeamMemberRole.Admin)]
        public async Task UpdateTeamMemberRoleAsync_Skip(TeamMemberRole role)
        {
            TeamMemberEntity teamMember = new TeamMemberEntity { Role = role };
            CancellationToken cancellationToken = CancellationToken.None;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            var storageContext = new Mock<IStorageContext>();

            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.UpdateTeamMemberRoleAsync(teamMember, role, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
        }

        [TestCase(TeamMemberRole.Member, TeamMemberRole.Admin)]
        [TestCase(TeamMemberRole.Admin, TeamMemberRole.Member)]
        public async Task UpdateTeamMemberRoleAsync_Succeeded(TeamMemberRole oldRole, TeamMemberRole newRole)
        {
            TeamMemberEntity teamMember = new TeamMemberEntity { Role = oldRole };
            CancellationToken cancellationToken = CancellationToken.None;
            TeamMemberEntity captured = null;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(t => t.MergeAsync(It.IsAny<TeamMemberEntity>(), cancellationToken))
                .Callback<TeamMemberEntity, CancellationToken>((tm, c) => { captured = tm; });
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);


            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            var result = await teamManagement.UpdateTeamMemberRoleAsync(teamMember, newRole, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
            Assert.IsNull(result.Description);
            Assert.IsNull(captured.Description);
            Assert.AreEqual(newRole, result.Role);
            Assert.AreEqual(newRole, captured.Role);
        }

        [Test]
        public async Task DeleteTeamMemberStatusAsync()
        {
            TeamMemberEntity teamMember = new TeamMemberEntity { PartitionKey = "pk", RowKey = "rk" };
            CancellationToken cancellationToken = CancellationToken.None;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(t => t.DeleteAsync("pk", "rk", cancellationToken));
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);


            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            await teamManagement.DeleteTeamMemberAsync(teamMember, cancellationToken);

            Mock.VerifyAll(logger, teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
        }

        [Test]
        public async Task DeleteTeamAsync()
        {
            TeamEntity team = new TeamEntity { PartitionKey = "pk", RowKey = "rk" };
            CancellationToken cancellationToken = CancellationToken.None;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamTable = new Mock<ITeamTable>();
            teamTable.Setup(t => t.DeleteAsync("pk", "rk", cancellationToken));
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamTable).Returns(teamTable.Object);


            TeamManagement teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object,
            };
            await teamManagement.DeleteTeamAsync(team, cancellationToken);

            Mock.VerifyAll(logger, teamTable, storageContext);
            teamTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();
        }

        [Test]
        public async Task ListPaginatedTeamsAsync_NoOptions()
        {
            string hackName = "foo";
            TeamQueryOptions options = null;
            CancellationToken cancellationToken = CancellationToken.None;
            var entities = MockHelper.CreateTableQuerySegment<TeamEntity>(
                 new List<TeamEntity>
                 {
                     new TeamEntity{  PartitionKey="pk" }
                 },
                 new TableContinuationToken { NextPartitionKey = "np", NextRowKey = "nr" }
                );

            TableQuery<TeamEntity> tableQueryCaptured = null;
            TableContinuationToken tableContinuationTokenCapatured = null;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamTable = new Mock<ITeamTable>();
            teamTable.Setup(p => p.ExecuteQuerySegmentedAsync(It.IsAny<TableQuery<TeamEntity>>(), It.IsAny<TableContinuationToken>(), cancellationToken))
                .Callback<TableQuery<TeamEntity>, TableContinuationToken, CancellationToken>((query, c, _) =>
                {
                    tableQueryCaptured = query;
                    tableContinuationTokenCapatured = c;
                })
                .ReturnsAsync(entities);
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamTable).Returns(teamTable.Object);

            var teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object
            };
            var segment = await teamManagement.ListPaginatedTeamsAsync(hackName, options, cancellationToken);

            Mock.VerifyAll(teamTable, storageContext);
            teamTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();

            Assert.AreEqual(1, segment.Count());
            Assert.AreEqual("pk", segment.First().HackathonName);
            Assert.AreEqual("np", segment.ContinuationToken.NextPartitionKey);
            Assert.AreEqual("nr", segment.ContinuationToken.NextRowKey);
            Assert.IsNull(tableContinuationTokenCapatured);
            Assert.AreEqual("PartitionKey eq 'foo'", tableQueryCaptured.FilterString);
            Assert.AreEqual(100, tableQueryCaptured.TakeCount.Value);
        }

        [TestCase(5, 5)]
        [TestCase(-1, 100)]
        public async Task ListPaginatedTeamsAsync_Options(int topInPara, int expectedTop)
        {
            string hackName = "foo";
            TeamQueryOptions options = new TeamQueryOptions
            {
                TableContinuationToken = new TableContinuationToken { NextPartitionKey = "np", NextRowKey = "nr" },
                Top = topInPara,
            };
            CancellationToken cancellationToken = CancellationToken.None;
            var entities = MockHelper.CreateTableQuerySegment<TeamEntity>(
                 new List<TeamEntity>
                 {
                     new TeamEntity{  PartitionKey="pk" }
                 },
                 new TableContinuationToken { NextPartitionKey = "np2", NextRowKey = "nr2" }
                );

            TableQuery<TeamEntity> tableQueryCaptured = null;
            TableContinuationToken tableContinuationTokenCapatured = null;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamTable = new Mock<ITeamTable>();
            teamTable.Setup(p => p.ExecuteQuerySegmentedAsync(It.IsAny<TableQuery<TeamEntity>>(), It.IsAny<TableContinuationToken>(), cancellationToken))
                .Callback<TableQuery<TeamEntity>, TableContinuationToken, CancellationToken>((query, c, _) =>
                {
                    tableQueryCaptured = query;
                    tableContinuationTokenCapatured = c;
                })
                .ReturnsAsync(entities);
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamTable).Returns(teamTable.Object);

            var teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object
            };
            var segment = await teamManagement.ListPaginatedTeamsAsync(hackName, options, cancellationToken);

            Mock.VerifyAll(teamTable, storageContext);
            teamTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();

            Assert.AreEqual(1, segment.Count());
            Assert.AreEqual("pk", segment.First().HackathonName);
            Assert.AreEqual("np2", segment.ContinuationToken.NextPartitionKey);
            Assert.AreEqual("nr2", segment.ContinuationToken.NextRowKey);
            Assert.AreEqual("np", tableContinuationTokenCapatured.NextPartitionKey);
            Assert.AreEqual("nr", tableContinuationTokenCapatured.NextRowKey);
            Assert.AreEqual("PartitionKey eq 'foo'", tableQueryCaptured.FilterString);
            Assert.AreEqual(expectedTop, tableQueryCaptured.TakeCount.Value);
        }

        private static IEnumerable ListPaginatedEnrollmentsAsyncTestData()
        {
            // arg0: options
            // arg1: expected top
            // arg2: expected query

            // no options
            yield return new TestCaseData(
                null,
                100,
                "PartitionKey eq 'foo'"
                );

            // top
            yield return new TestCaseData(
                new TeamMemberQueryOptions { Top = 5 },
                5,
                "PartitionKey eq 'foo'"
                );
            yield return new TestCaseData(
               new TeamMemberQueryOptions { Top = -1 },
               100,
               "PartitionKey eq 'foo'"
               );

            // status
            yield return new TestCaseData(
               new TeamMemberQueryOptions { Status = TeamMemberStatus.approved },
               100,
               "(PartitionKey eq 'foo') and (Status eq 1)"
               );
            yield return new TestCaseData(
               new TeamMemberQueryOptions { Status = TeamMemberStatus.pendingApproval },
               100,
               "(PartitionKey eq 'foo') and (Status eq 0)"
               );

            // Role
            yield return new TestCaseData(
               new TeamMemberQueryOptions { Role = TeamMemberRole.Admin },
               100,
               "(PartitionKey eq 'foo') and (Role eq 0)"
               );
            yield return new TestCaseData(
               new TeamMemberQueryOptions { Role = TeamMemberRole.Member },
               100,
               "(PartitionKey eq 'foo') and (Role eq 1)"
               );

            // all options
            yield return new TestCaseData(
               new TeamMemberQueryOptions
               {
                   Top = 20,
                   Role = TeamMemberRole.Member,
                   Status = TeamMemberStatus.approved,
               },
               20,
               "((PartitionKey eq 'foo') and (Status eq 1)) and (Role eq 1)"
               );
        }

        [Test, TestCaseSource(nameof(ListPaginatedEnrollmentsAsyncTestData))]
        public async Task ListPaginatedEnrollmentsAsync_Options(TeamMemberQueryOptions options, int expectedTop, string expectedFilter)
        {
            string hackName = "foo";
            CancellationToken cancellationToken = CancellationToken.None;
            var entities = MockHelper.CreateTableQuerySegment<TeamMemberEntity>(
                 new List<TeamMemberEntity>
                 {
                     new TeamMemberEntity{  PartitionKey="pk" }
                 },
                 new TableContinuationToken { NextPartitionKey = "np", NextRowKey = "nr" }
                );

            TableQuery<TeamMemberEntity> tableQueryCaptured = null;
            TableContinuationToken tableContinuationTokenCapatured = null;

            var logger = new Mock<ILogger<TeamManagement>>();
            var teamMemberTable = new Mock<ITeamMemberTable>();
            teamMemberTable.Setup(p => p.ExecuteQuerySegmentedAsync(It.IsAny<TableQuery<TeamMemberEntity>>(), It.IsAny<TableContinuationToken>(), cancellationToken))
                .Callback<TableQuery<TeamMemberEntity>, TableContinuationToken, CancellationToken>((query, c, _) =>
                {
                    tableQueryCaptured = query;
                    tableContinuationTokenCapatured = c;
                })
                .ReturnsAsync(entities);
            var storageContext = new Mock<IStorageContext>();
            storageContext.SetupGet(p => p.TeamMemberTable).Returns(teamMemberTable.Object);

            var teamManagement = new TeamManagement(logger.Object)
            {
                StorageContext = storageContext.Object
            };
            var segment = await teamManagement.ListPaginatedTeamMembersAsync(hackName, options, cancellationToken);

            Mock.VerifyAll(teamMemberTable, storageContext);
            teamMemberTable.VerifyNoOtherCalls();
            storageContext.VerifyNoOtherCalls();

            Assert.AreEqual(1, segment.Count());
            Assert.AreEqual("pk", segment.First().TeamId);
            Assert.AreEqual("np", segment.ContinuationToken.NextPartitionKey);
            Assert.AreEqual("nr", segment.ContinuationToken.NextRowKey);
            Assert.IsNull(tableContinuationTokenCapatured);
            Assert.AreEqual(expectedFilter, tableQueryCaptured.FilterString);
            Assert.AreEqual(expectedTop, tableQueryCaptured.TakeCount.Value);
        }
    }
}
