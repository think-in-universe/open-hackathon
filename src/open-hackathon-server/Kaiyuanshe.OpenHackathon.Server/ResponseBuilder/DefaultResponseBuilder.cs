﻿using Kaiyuanshe.OpenHackathon.Server.Models;
using Kaiyuanshe.OpenHackathon.Server.Storage.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kaiyuanshe.OpenHackathon.Server.ResponseBuilder
{
    public interface IResponseBuilder
    {
        Hackathon BuildHackathon(HackathonEntity hackathonEntity, HackathonRoles roles);

        HackathonList BuildHackathonList(IEnumerable<HackathonEntity> hackathonEntities, string nextLink);

        Enrollment BuildEnrollment(EnrollmentEntity participant);

        Team BuildTeam(TeamEntity teamEntity);

        TeamMember BuildTeamMember(TeamMemberEntity teamMemberEntity);

        Award BuildAward(AwardEntity awardEntity);

        TResult BuildResourceList<TSrcItem, TResultItem, TResult>(IEnumerable<TSrcItem> items, Func<TSrcItem, TResultItem> converter, string nextLink)
            where TResult : IResourceList<TResultItem>, new();
    }

    public class DefaultResponseBuilder : IResponseBuilder
    {
        public Hackathon BuildHackathon(HackathonEntity hackathonEntity, HackathonRoles roles)
        {
            return hackathonEntity.As<Hackathon>(h =>
            {
                h.updatedAt = hackathonEntity.Timestamp.DateTime;
                h.roles = roles;
            });
        }

        public HackathonList BuildHackathonList(IEnumerable<HackathonEntity> hackathonEntities, string nextLink)
        {
            return new HackathonList
            {
                value = hackathonEntities.Select(h => BuildHackathon(h, null)).ToArray(),
                nextLink = nextLink
            };
        }

        public Enrollment BuildEnrollment(EnrollmentEntity participant)
        {
            return participant.As<Enrollment>(p =>
            {
                p.updatedAt = participant.Timestamp.DateTime;
            });
        }

        public Team BuildTeam(TeamEntity teamEntity)
        {
            return teamEntity.As<Team>(p =>
            {
                p.updatedAt = teamEntity.Timestamp.DateTime;
            });
        }

        public TeamMember BuildTeamMember(TeamMemberEntity teamMemberEntity)
        {
            return teamMemberEntity.As<TeamMember>(p =>
            {
                p.updatedAt = teamMemberEntity.Timestamp.DateTime;
            });
        }

        public TResult BuildResourceList<TSrcItem, TResultItem, TResult>(IEnumerable<TSrcItem> items, Func<TSrcItem, TResultItem> converter, string nextLink)
            where TResult : IResourceList<TResultItem>, new()
        {
            return new TResult
            {
                value = items.Select(p => converter(p)).ToArray(),
                nextLink = nextLink,
            };
        }

        public Award BuildAward(AwardEntity awardEntity)
        {
            return awardEntity.As<Award>(p =>
            {
                p.updatedAt = awardEntity.Timestamp.DateTime;
            });
        }
    }
}
