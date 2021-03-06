﻿/*
 * Original work Copyright (c) 2016 Lokra Studio
 * Url: https://github.com/lokra/seaweedfs-client
 * Modified work Copyright 2017 smartbox software solutions
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Runtime.Serialization;
using Newtonsoft.Json;
using smartbox.SeaweedFs.Client.Utils;

namespace smartbox.SeaweedFs.Client.Core.Infrastructure
{
    public class MasterStatus
    {
        private volatile bool _isActive;

        public MasterStatus(string url)
        {
            Url = ConnectionUtil.ConvertUrlWithScheme(url);
        }

        [JsonProperty("Url")]
        public string Url { get; private set; }

        [JsonProperty("IsActive")]
        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; }
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            Url = ConnectionUtil.ConvertUrlWithScheme(Url);
        }

        public override int GetHashCode()
        {
            var result = Url.GetHashCode();
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj) return true;

            if (!(obj is MasterStatus that)) return false;
            
            return Url.Equals(that.Url);
        }

        public override string ToString()
        {
            return "MasterStatus{" +
                   "url=" + Url +
                   ", isActive=" + IsActive +
                   '}';
        }
    }
}
