﻿/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Text;

namespace OpenHardwareMonitor.Hardware.CPU
{

    internal enum Vendor
    {
        Unknown,
        Intel,
        AMD,
    }

    internal class CPUID
    {
        private readonly uint threadMaskWith;
        private readonly uint coreMaskWith;
        public const uint CPUID_0 = 0;
        public const uint CPUID_EXT = 0x80000000;

        private static void AppendRegister(StringBuilder b, uint value)
        {
            b.Append((char)((value) & 0xff));
            b.Append((char)((value >> 8) & 0xff));
            b.Append((char)((value >> 16) & 0xff));
            b.Append((char)((value >> 24) & 0xff));
        }

        private static uint NextLog2(long x)
        {
            if (x <= 0)
                return 0;

            x--;
            uint count = 0;
            while (x > 0)
            {
                x >>= 1;
                count++;
            }

            return count;
        }

        public static CPUID Get(int group, int thread)
        {
            if (thread >= 64)
                return null;

            var affinity = GroupAffinity.Single((ushort)group, thread);

            GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
            if (previousAffinity == GroupAffinity.Undefined)
                return null;

            try
            {
                return new CPUID(group, thread, affinity);
            }
            finally
            {
                ThreadAffinity.Set(previousAffinity);
            }
        }

        private CPUID(int group, int thread, GroupAffinity affinity)
        {
            Group = group;
            Thread = thread;
            Affinity = affinity;

            uint maxCpuid = 0;
            uint maxCpuidExt = 0;


            Opcode.Cpuid(CPUID_0, 0, out uint eax, out uint ebx, out uint ecx, out uint edx);
            if (eax > 0)
                maxCpuid = eax;
            else
                return;

            StringBuilder vendorBuilder = new StringBuilder();
            AppendRegister(vendorBuilder, ebx);
            AppendRegister(vendorBuilder, edx);
            AppendRegister(vendorBuilder, ecx);
            string cpuVendor = vendorBuilder.ToString();
            switch (cpuVendor)
            {
                case "GenuineIntel":
                    Vendor = Vendor.Intel;
                    break;
                case "AuthenticAMD":
                    Vendor = Vendor.AMD;
                    break;
                default:
                    Vendor = Vendor.Unknown;
                    break;
            }
            eax = ebx = ecx = edx = 0;
            Opcode.Cpuid(CPUID_EXT, 0, out eax, out ebx, out ecx, out edx);
            if (eax > CPUID_EXT)
                maxCpuidExt = eax - CPUID_EXT;
            else
                return;

            maxCpuid = Math.Min(maxCpuid, 1024);
            maxCpuidExt = Math.Min(maxCpuidExt, 1024);

            Data = new uint[maxCpuid + 1, 4];
            for (uint i = 0; i < (maxCpuid + 1); i++)
            {
                Opcode.Cpuid(CPUID_0 + i, 0,
                  out Data[i, 0], out Data[i, 1],
                  out Data[i, 2], out Data[i, 3]);
            }

            ExtData = new uint[maxCpuidExt + 1, 4];
            for (uint i = 0; i < (maxCpuidExt + 1); i++)
            {
                Opcode.Cpuid(CPUID_EXT + i, 0,
                  out ExtData[i, 0], out ExtData[i, 1],
                  out ExtData[i, 2], out ExtData[i, 3]);
            }

            StringBuilder nameBuilder = new StringBuilder();
            for (uint i = 2; i <= 4; i++)
            {
                Opcode.Cpuid(CPUID_EXT + i, 0, out eax, out ebx, out ecx, out edx);
                AppendRegister(nameBuilder, eax);
                AppendRegister(nameBuilder, ebx);
                AppendRegister(nameBuilder, ecx);
                AppendRegister(nameBuilder, edx);
            }
            nameBuilder.Replace('\0', ' ');
            BrandString = nameBuilder.ToString().Trim();
            nameBuilder.Replace("Dual-Core Processor", "");
            nameBuilder.Replace("Triple-Core Processor", "");
            nameBuilder.Replace("Quad-Core Processor", "");
            nameBuilder.Replace("Six-Core Processor", "");
            nameBuilder.Replace("Eight-Core Processor", "");
            nameBuilder.Replace("Dual Core Processor", "");
            nameBuilder.Replace("Quad Core Processor", "");
            nameBuilder.Replace("12-Core Processor", "");
            nameBuilder.Replace("16-Core Processor", "");
            nameBuilder.Replace("24-Core Processor", "");
            nameBuilder.Replace("32-Core Processor", "");
            nameBuilder.Replace("64-Core Processor", "");
            nameBuilder.Replace("6-Core Processor", "");
            nameBuilder.Replace("8-Core Processor", "");
            nameBuilder.Replace("with Radeon Vega Mobile Gfx", "");
            nameBuilder.Replace("w/ Radeon Vega Mobile Gfx", "");
            nameBuilder.Replace("with Radeon Vega Graphics", "");
            nameBuilder.Replace("with Radeon Graphics", "");
            nameBuilder.Replace("APU with Radeon(tm) HD Graphics", "");
            nameBuilder.Replace("APU with Radeon(TM) HD Graphics", "");
            nameBuilder.Replace("APU with AMD Radeon R2 Graphics", "");
            nameBuilder.Replace("APU with AMD Radeon R3 Graphics", "");
            nameBuilder.Replace("APU with AMD Radeon R4 Graphics", "");
            nameBuilder.Replace("APU with AMD Radeon R5 Graphics", "");
            nameBuilder.Replace("APU with Radeon(tm) R3", "");
            nameBuilder.Replace("RADEON R2, 4 COMPUTE CORES 2C+2G", "");
            nameBuilder.Replace("RADEON R4, 5 COMPUTE CORES 2C+3G", "");
            nameBuilder.Replace("RADEON R5, 5 COMPUTE CORES 2C+3G", "");
            nameBuilder.Replace("RADEON R5, 10 COMPUTE CORES 4C+6G", "");
            nameBuilder.Replace("RADEON R7, 10 COMPUTE CORES 4C+6G", "");
            nameBuilder.Replace("RADEON R7, 12 COMPUTE CORES 4C+8G", "");
            nameBuilder.Replace("Radeon R5, 6 Compute Cores 2C+4G", "");
            nameBuilder.Replace("Radeon R5, 8 Compute Cores 4C+4G", "");
            nameBuilder.Replace("Radeon R6, 10 Compute Cores 4C+6G", "");
            nameBuilder.Replace("Radeon R7, 10 Compute Cores 4C+6G", "");
            nameBuilder.Replace("Radeon R7, 12 Compute Cores 4C+8G", "");
            nameBuilder.Replace("R5, 10 Compute Cores 4C+6G", "");
            nameBuilder.Replace("R7, 12 COMPUTE CORES 4C+8G", "");
            nameBuilder.Replace("(R)", " ");
            nameBuilder.Replace("(TM)", " ");
            nameBuilder.Replace("(tm)", " ");
            nameBuilder.Replace("CPU", " ");

            for (int i = 0; i < 10; i++) nameBuilder.Replace("  ", " ");
            Name = nameBuilder.ToString();
            if (Name.Contains("@"))
                Name = Name.Remove(Name.LastIndexOf('@'));
            Name = Name.Trim();

            Family = ((Data[1, 0] & 0x0FF00000) >> 20) +
              ((Data[1, 0] & 0x0F00) >> 8);
            Model = ((Data[1, 0] & 0x0F0000) >> 12) +
              ((Data[1, 0] & 0xF0) >> 4);
            Stepping = Data[1, 0] & 0x0F;

            ApicId = (Data[1, 1] >> 24) & 0xFF;

            switch (Vendor)
            {
                case Vendor.Intel:
                    uint maxCoreAndThreadIdPerPackage = (Data[1, 1] >> 16) & 0xFF;
                    uint maxCoreIdPerPackage;
                    if (maxCpuid >= 4)
                        maxCoreIdPerPackage = ((Data[4, 0] >> 26) & 0x3F) + 1;
                    else
                        maxCoreIdPerPackage = 1;
                    threadMaskWith =
                      NextLog2(maxCoreAndThreadIdPerPackage / maxCoreIdPerPackage);
                    coreMaskWith = NextLog2(maxCoreIdPerPackage);
                    break;
                case Vendor.AMD:
                    if (Family == 0x17 || Family == 0x19)
                    {
                        coreMaskWith = (ExtData[8, 2] >> 12) & 0xF;
                        threadMaskWith =
                          NextLog2(((ExtData[0x1E, 1] >> 8) & 0xFF) + 1);
                    }
                    else
                    {
                        uint corePerPackage;
                        if (maxCpuidExt >= 8)
                            corePerPackage = (ExtData[8, 2] & 0xFF) + 1;
                        else
                            corePerPackage = 1;
                        coreMaskWith = NextLog2(corePerPackage);
                        threadMaskWith = 0;
                    }
                    break;
                default:
                    threadMaskWith = 0;
                    coreMaskWith = 0;
                    break;
            }

            ProcessorId = ApicId >> (int)(coreMaskWith + threadMaskWith);
            CoreId = (ApicId >> (int)threadMaskWith)
              - (ProcessorId << (int)coreMaskWith);
            ThreadId = ApicId
              - (ProcessorId << (int)(coreMaskWith + threadMaskWith))
              - (CoreId << (int)threadMaskWith);
        }

        public string Name { get; } = "";

        public string BrandString { get; } = "";

        public int Group { get; }

        public int Thread { get; }

        public GroupAffinity Affinity { get; }

        public Vendor Vendor { get; } = Vendor.Unknown;

        public uint Family { get; }

        public uint Model { get; }

        public uint Stepping { get; }

        public uint ApicId { get; }

        public uint ProcessorId { get; }

        public uint CoreId { get; }

        public uint ThreadId { get; }

        public uint[,] Data { get; } = new uint[0, 0];

        public uint[,] ExtData { get; } = new uint[0, 0];
    }
}
