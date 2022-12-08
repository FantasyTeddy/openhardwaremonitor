/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.LPC
{
    internal class IT87XX : ISuperIO
    {

        private readonly ushort address;
        private readonly byte version;

        private readonly ushort gpioAddress;
        private readonly int gpioCount;

        private readonly ushort addressReg;
        private readonly ushort dataReg;
        private readonly float voltageGain;
        private readonly bool has16bitFanCounter;

        // Consts
        private const byte ITE_VENDOR_ID = 0x90;

        // Environment Controller
        private const byte ADDRESS_REGISTER_OFFSET = 0x05;
        private const byte DATA_REGISTER_OFFSET = 0x06;

        // Environment Controller Registers    
        private const byte CONFIGURATION_REGISTER = 0x00;
        private const byte TEMPERATURE_BASE_REG = 0x29;
        private const byte VENDOR_ID_REGISTER = 0x58;
        private const byte FAN_TACHOMETER_DIVISOR_REGISTER = 0x0B;
        private readonly byte[] FAN_TACHOMETER_REG =
          { 0x0d, 0x0e, 0x0f, 0x80, 0x82 };
        private readonly byte[] FAN_TACHOMETER_EXT_REG =
          { 0x18, 0x19, 0x1a, 0x81, 0x83 };
        private const byte VOLTAGE_BASE_REG = 0x20;
        private const byte FAN_MAIN_CTRL_REG = 0x13;
        private readonly byte[] FAN_PWM_CTRL_REG;
        private readonly byte[] FAN_PWM_CTRL_EXT_REG =
          { 0x63, 0x6b, 0x73, 0x7b, 0xa3 };

        private readonly bool[] restoreDefaultFanPwmControlRequired = new bool[5];
        private readonly bool[] initialFanOutputModeEnabled = new bool[3];
        private readonly byte[] initialFanPwmControl = new byte[5];
        private readonly byte[] initialFanPwmControlExt = new byte[5];

        private byte ReadByte(byte register, out bool valid)
        {
            Ring0.WriteIoPort(addressReg, register);
            byte value = Ring0.ReadIoPort(dataReg);
            if (Chip == Chip.IT8688E)
                valid = true;
            else
                valid = register == Ring0.ReadIoPort(addressReg);
            return value;
        }

        private bool WriteByte(byte register, byte value)
        {
            Ring0.WriteIoPort(addressReg, register);
            Ring0.WriteIoPort(dataReg, value);
            return register == Ring0.ReadIoPort(addressReg);
        }

        public byte? ReadGPIO(int index)
        {
            if (index >= gpioCount)
                return null;

            return Ring0.ReadIoPort((ushort)(gpioAddress + index));
        }

        public void WriteGPIO(int index, byte value)
        {
            if (index >= gpioCount)
                return;

            Ring0.WriteIoPort((ushort)(gpioAddress + index), value);
        }

        private void SaveDefaultFanPwmControl(int index)
        {
            if (!restoreDefaultFanPwmControlRequired[index])
            {
                initialFanPwmControl[index] = ReadByte(FAN_PWM_CTRL_REG[index], out _);

                if (index < 3)
                {
                    initialFanOutputModeEnabled[index] =
                      (ReadByte(FAN_MAIN_CTRL_REG, out _) & (1 << index)) > 0;
                }

                if (Chip == Chip.IT8721F ||
                    Chip == Chip.IT8665E ||
                    Chip == Chip.IT8686E ||
                    Chip == Chip.IT8688E ||
                    Chip == Chip.IT879XE)
                {
                    initialFanPwmControlExt[index] =
                      ReadByte(FAN_PWM_CTRL_EXT_REG[index], out _);
                }
                restoreDefaultFanPwmControlRequired[index] = true;
            }
        }

        private void RestoreDefaultFanPwmControl(int index)
        {
            if (restoreDefaultFanPwmControlRequired[index])
            {
                WriteByte(FAN_PWM_CTRL_REG[index], initialFanPwmControl[index]);

                if (index < 3)
                {
                    byte value = ReadByte(FAN_MAIN_CTRL_REG, out _);

                    if (((value & (1 << index)) > 0) != initialFanOutputModeEnabled[index])
                    {
                        WriteByte(FAN_MAIN_CTRL_REG, (byte)(value ^ (1 << index)));
                    }
                }

                if (Chip == Chip.IT8721F ||
                    Chip == Chip.IT8665E ||
                    Chip == Chip.IT8686E ||
                    Chip == Chip.IT8688E ||
                    Chip == Chip.IT879XE)
                {
                    WriteByte(FAN_PWM_CTRL_EXT_REG[index], initialFanPwmControlExt[index]);
                }
                restoreDefaultFanPwmControlRequired[index] = false;
            }
        }

        public void SetControl(int index, byte? value)
        {
            if (index < 0 || index >= Controls.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (!Ring0.WaitIsaBusMutex(10))
                return;

            if (value.HasValue)
            {
                SaveDefaultFanPwmControl(index);

                if (index < 3)
                {
                    if (!initialFanOutputModeEnabled[index])
                    {
                        WriteByte(FAN_MAIN_CTRL_REG,
                          (byte)(ReadByte(FAN_MAIN_CTRL_REG, out _) | (1 << index)));
                    }
                }

                if (Chip == Chip.IT8721F ||
                    Chip == Chip.IT8665E ||
                    Chip == Chip.IT8686E ||
                    Chip == Chip.IT8688E ||
                    Chip == Chip.IT879XE)
                {
                    WriteByte(FAN_PWM_CTRL_REG[index],
                      (byte)(initialFanPwmControl[index] & 0x7F));
                    WriteByte(FAN_PWM_CTRL_EXT_REG[index], value.Value);
                }
                else
                {
                    WriteByte(FAN_PWM_CTRL_REG[index], (byte)(value.Value >> 1));
                }
            }
            else
            {
                RestoreDefaultFanPwmControl(index);
            }

            Ring0.ReleaseIsaBusMutex();
        }

        public IT87XX(Chip chip, ushort address, ushort gpioAddress, byte version)
        {

            this.address = address;
            Chip = chip;
            this.version = version;
            this.addressReg = (ushort)(address + ADDRESS_REGISTER_OFFSET);
            this.dataReg = (ushort)(address + DATA_REGISTER_OFFSET);
            this.gpioAddress = gpioAddress;

            // Check vendor id
            byte vendorId = ReadByte(VENDOR_ID_REGISTER, out bool valid);
            if (!valid || vendorId != ITE_VENDOR_ID)
                return;

            // Bit 0x10 of the configuration register should always be 1
            byte configuration = ReadByte(CONFIGURATION_REGISTER, out valid);
            if ((configuration & 0x10) == 0 &&
              chip != Chip.IT8655E && chip != Chip.IT8665E)
            {
                return;
            }

            if (!valid)
                return;

            if (chip == Chip.IT8665E)
            {
                FAN_PWM_CTRL_REG = new byte[] { 0x15, 0x16, 0x17, 0x1e, 0x1f };
            }
            else
            {
                FAN_PWM_CTRL_REG = new byte[] { 0x15, 0x16, 0x17, 0x7f, 0xa7 };
            }

            switch (chip)
            {
                case Chip.IT8665E:
                case Chip.IT8686E:
                case Chip.IT8688E:
                    Voltages = new float?[9];
                    Temperatures = new float?[6];
                    Fans = new float?[5];
                    Controls = new float?[5];
                    break;
                case Chip.IT8655E:
                    Voltages = new float?[9];
                    Temperatures = new float?[6];
                    Fans = new float?[3];
                    break;
                case Chip.IT879XE:
                    Voltages = new float?[9];
                    Temperatures = new float?[3];
                    Fans = new float?[3];
                    Controls = new float?[3];
                    break;
                case Chip.IT8705F:
                    Voltages = new float?[9];
                    Temperatures = new float?[3];
                    Fans = new float?[3];
                    Controls = new float?[3];
                    break;
                default:
                    Voltages = new float?[9];
                    Temperatures = new float?[3];
                    Fans = new float?[5];
                    Controls = new float?[3];
                    break;
            }

            // set the voltage for the ADC LSB 
            switch (chip)
            {
                case Chip.IT8620E:
                case Chip.IT8628E:
                case Chip.IT8686E:
                case Chip.IT8688E:
                case Chip.IT8721F:
                case Chip.IT8728F:
                case Chip.IT8771E:
                case Chip.IT8772E:
                    voltageGain = 0.012f;
                    break;
                case Chip.IT8655E:
                case Chip.IT8665E:
                case Chip.IT879XE:
                    voltageGain = 0.011f;
                    break;
                default:
                    voltageGain = 0.016f;
                    break;
            }

            // older IT8705F and IT8721F revisions do not have 16-bit fan counters
            if ((chip == Chip.IT8705F && version < 3) ||
                (chip == Chip.IT8712F && version < 8))
            {
                has16bitFanCounter = false;
            }
            else
            {
                has16bitFanCounter = true;
            }

            // Set the number of GPIO sets
            switch (chip)
            {
                case Chip.IT8712F:
                case Chip.IT8716F:
                case Chip.IT8718F:
                case Chip.IT8726F:
                    gpioCount = 5;
                    break;
                case Chip.IT8720F:
                case Chip.IT8721F:
                    gpioCount = 8;
                    break;
                default:
                    gpioCount = 0;
                    break;
            }
        }

        public Chip Chip { get; }
        public float?[] Voltages { get; } = Array.Empty<float?>();
        public float?[] Temperatures { get; } = Array.Empty<float?>();
        public float?[] Fans { get; } = Array.Empty<float?>();
        public float?[] Controls { get; } = Array.Empty<float?>();

        public string GetReport()
        {
            StringBuilder r = new StringBuilder();

            r.AppendLine("LPC " + GetType().Name);
            r.AppendLine();
            r.Append("Chip ID: 0x"); r.AppendLine(Chip.ToString("X"));
            r.Append("Chip Version: 0x"); r.AppendLine(
              version.ToString("X", CultureInfo.InvariantCulture));
            r.Append("Base Address: 0x"); r.AppendLine(
              address.ToString("X4", CultureInfo.InvariantCulture));
            r.Append("GPIO Address: 0x"); r.AppendLine(
              gpioAddress.ToString("X4", CultureInfo.InvariantCulture));
            r.AppendLine();

            if (!Ring0.WaitIsaBusMutex(100))
                return r.ToString();

            r.AppendLine("Environment Controller Registers");
            r.AppendLine();
            r.AppendLine("      00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
            r.AppendLine();
            for (int i = 0; i <= 0xA; i++)
            {
                r.Append(' ');
                r.Append((i << 4).ToString("X2", CultureInfo.InvariantCulture));
                r.Append("  ");
                for (int j = 0; j <= 0xF; j++)
                {
                    r.Append(' ');
                    byte value = ReadByte((byte)((i << 4) | j), out bool valid);
                    r.Append(
                      valid ? value.ToString("X2", CultureInfo.InvariantCulture) : "??");
                }
                r.AppendLine();
            }
            r.AppendLine();

            r.AppendLine("GPIO Registers");
            r.AppendLine();
            for (int i = 0; i < gpioCount; i++)
            {
                r.Append(' ');
                r.Append(ReadGPIO(i).Value.ToString("X2",
                  CultureInfo.InvariantCulture));
            }
            r.AppendLine();
            r.AppendLine();

            Ring0.ReleaseIsaBusMutex();

            return r.ToString();
        }

        public void Update()
        {
            if (!Ring0.WaitIsaBusMutex(10))
                return;

            for (int i = 0; i < Voltages.Length; i++)
            {

                float value =
                  voltageGain * ReadByte((byte)(VOLTAGE_BASE_REG + i), out bool valid);

                if (!valid)
                    continue;
                if (value > 0)
                    Voltages[i] = value;
                else
                    Voltages[i] = null;
            }

            for (int i = 0; i < Temperatures.Length; i++)
            {
                sbyte value = (sbyte)ReadByte(
                  (byte)(TEMPERATURE_BASE_REG + i), out bool valid);
                if (!valid)
                    continue;

                if (value < sbyte.MaxValue && value > 0)
                    Temperatures[i] = value;
                else
                    Temperatures[i] = null;
            }

            if (has16bitFanCounter)
            {
                for (int i = 0; i < Fans.Length; i++)
                {
                    int value = ReadByte(FAN_TACHOMETER_REG[i], out bool valid);
                    if (!valid)
                        continue;
                    value |= ReadByte(FAN_TACHOMETER_EXT_REG[i], out valid) << 8;
                    if (!valid)
                        continue;

                    if (value > 0x3f)
                    {
                        Fans[i] = (value < 0xffff) ? 1.35e6f / (value * 2) : 0;
                    }
                    else
                    {
                        Fans[i] = null;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Fans.Length; i++)
                {
                    int value = ReadByte(FAN_TACHOMETER_REG[i], out bool valid);
                    if (!valid)
                        continue;

                    int divisor = 2;
                    if (i < 2)
                    {
                        int divisors = ReadByte(FAN_TACHOMETER_DIVISOR_REGISTER, out valid);
                        if (!valid)
                            continue;
                        divisor = 1 << ((divisors >> (3 * i)) & 0x7);
                    }

                    if (value > 0)
                    {
                        Fans[i] = (value < 0xff) ? 1.35e6f / (value * divisor) : 0;
                    }
                    else
                    {
                        Fans[i] = null;
                    }
                }
            }

            for (int i = 0; i < Controls.Length; i++)
            {
                byte value = ReadByte(FAN_PWM_CTRL_REG[i], out bool valid);
                if (!valid)
                    continue;

                if ((value & 0x80) > 0)
                {
                    // automatic operation (value can't be read)
                    Controls[i] = null;
                }
                else
                {
                    // software operation
                    if (Chip == Chip.IT8721F ||
                        Chip == Chip.IT8665E ||
                        Chip == Chip.IT8686E ||
                        Chip == Chip.IT8688E ||
                        Chip == Chip.IT879XE)
                    {
                        value = ReadByte(FAN_PWM_CTRL_EXT_REG[i], out valid);
                        if (valid)
                            Controls[i] = (float)Math.Round(value * 100.0f / 0xFF);
                    }
                    else
                    {
                        Controls[i] = (float)Math.Round((value & 0x7F) * 100.0f / 0x7F);
                    }
                }
            }

            Ring0.ReleaseIsaBusMutex();
        }
    }
}
