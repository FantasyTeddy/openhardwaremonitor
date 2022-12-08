/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2011 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

namespace OpenHardwareMonitor.Collections
{

    public struct Pair<TFirst, TSecond>
    {
        public Pair(TFirst first, TSecond second)
        {
            First = first;
            Second = second;
        }

        public TFirst First { get; set; }

        public TSecond Second { get; set; }

        public override int GetHashCode()
        {
            return (First != null ? First.GetHashCode() : 0) ^
              (Second != null ? Second.GetHashCode() : 0);
        }
    }
}
