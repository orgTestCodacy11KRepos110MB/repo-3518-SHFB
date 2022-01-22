﻿//===============================================================================================================
// System  : Sandcastle Tools - Sandcastle Tools Core Class Library
// File    : AppliedChangesEventArgs.cs
// Author  : Eric Woodruff  (Eric@EWoodruff.us)
// Updated : 01/18/2022
// Note    : Copyright 2022, Eric Woodruff, All rights reserved
//
// This file contains an event arguments class used by components to indicate that they have finished applying
// their changes to the given topic.
//
// This code is published under the Microsoft Public License (Ms-PL).  A copy of the license should be
// distributed with the code and can be found at the project website: https://GitHub.com/EWSoftware/SHFB.  This
// notice, the author's name, and all copyright notices must remain intact in all applications, documentation,
// and source files.
//
//    Date     Who  Comments
// ==============================================================================================================
// 01/18/2022  EFW  Replaced the TransformedTopicEventArgs class with this one and moved to Sandcastle.Core
//===============================================================================================================

using System;
using System.Xml;

namespace Sandcastle.Core.BuildAssembler.BuildComponent
{
    /// <summary>
    /// This is used by components to indicate that they have finished applying their changes to the given topic
    /// </summary>
    public class AppliedChangesEventArgs : EventArgs
    {
        /// <summary>
        /// This read-only property returns the group ID of the component that applied the changes
        /// </summary>
        public string GroupId { get; }

        /// <summary>
        /// This read-only property returns the ID of the component that applied the changes
        /// </summary>
        public string ComponentId { get; }

        /// <summary>
        /// This read-only property returns the topic key
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// This read-only property returns the topic document that was modified
        /// </summary>
        /// <remarks>Event handlers can further modify the topic's XML as needed</remarks>
        public XmlDocument Document { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="groupId">The group ID of the component</param>
        /// <param name="componentId">The component ID</param>
        /// <param name="key">The topic key</param>
        /// <param name="document">The topic document</param>
        public AppliedChangesEventArgs(string groupId, string componentId, string key, XmlDocument document)
        {
            this.GroupId = groupId;
            this.ComponentId = componentId;
            this.Key = key;
            this.Document = document;
        }
    }
}
