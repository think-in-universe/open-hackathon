﻿namespace Kaiyuanshe.OpenHackathon.Server.Models
{
    /// <summary>
    /// decribes a resource list
    /// </summary>
    public interface IResourceList<T>
    {
        /// <summary>
        /// List of the resources
        /// </summary>
        T[] value { get; set; }

        /// <summary>
        /// The URL the client should use to fetch the next page (per server side paging).
        /// No more results if it's null or empty.
        /// </summary>
        string nextLink { get; set; }
    }
}
