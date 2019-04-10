﻿using hjudgeWebHost.Data.Identity;
using System;

namespace hjudgeWebHost.Data
{
    public partial class GroupJoin
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public DateTime JoinTime { get; set; }

#nullable disable
        public UserInfo UserInfo { get; set; }
        public Group Group { get; set; }
#nullable enable
    }
}