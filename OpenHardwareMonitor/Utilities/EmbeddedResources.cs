/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Drawing;
using System.Reflection;

namespace OpenHardwareMonitor.Utilities
{
    public static class EmbeddedResources
    {
        public static Image GetImage(string name)
        {
            name = "OpenHardwareMonitor.Resources." + name;

            return new Bitmap(Assembly.GetExecutingAssembly().GetManifestResourceStream(name));
        }

        public static Icon GetIcon(string name)
        {
            name = "OpenHardwareMonitor.Resources." + name;

            return new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream(name));
        }
    }
}
