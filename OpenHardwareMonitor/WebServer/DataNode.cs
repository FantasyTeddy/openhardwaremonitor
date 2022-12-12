/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012 Prince Samuel <prince.samuel@gmail.com>
  Copyright (C) 2012-2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections.Generic;
using Newtonsoft.Json;

namespace OpenHardwareMonitor.WebServer
{
    public class DataNode
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        public string Text { get; set; }

        public List<DataNode> Children { get; set; }

        public string Min { get; set; }

        public string Value { get; set; }

        public string Max { get; set; }

        public string ImageURL { get; set; }
    }
}
