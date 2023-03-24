﻿using Newtonsoft.Json;
using PKHeX.Core;
using PokeViewer.NET.Properties;
using SysBot.Base;
using System.Text;
using static SysBot.Base.SwitchButton;
using static System.Buffers.Binary.BinaryPrimitives;
using PKHeX.Drawing.PokeSprite;
using static PokeViewer.NET.RoutineExecutor;

namespace PokeViewer.NET.SubForms
{
    public partial class MiscView : Form
    {
        private readonly ViewerExecutor Executor;
        public IReadOnlyList<long> TeraRaidBlockPointer { get; } = new long[] { 0x44A98C8, 0x180, 0x40 };
        private int[] IVFilters = Array.Empty<int>();
        private byte[]? CenterPOS = Array.Empty<byte>();
        private int V_Form;
        private Image MapSprite = null!;
        public MiscView(ViewerExecutor executor)
        {
            InitializeComponent();
            Executor = executor;
            StopOnSpecies.Text = Settings.Default.OutbreakSpecies;
            OverShoot.Value = Settings.Default.MiscOvershoot;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var token = CancellationToken.None;
            IVFilters = GrabIvFilters();
            while (!token.IsCancellationRequested)
            {
                if (HardStopEventScan.Checked)
                {
                    MessageBox.Show("HardStop enabled, ending task. Uncheck if you wish to scan until match is found.");
                    break;
                }
                var TeraRaidBlockOffset = await Executor.SwitchConnection.PointerAll(TeraRaidBlockPointer, token).ConfigureAwait(false);
                var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(TeraRaidBlockOffset, 36, token).ConfigureAwait(false);
                var seed = BitConverter.ToUInt32(data.Slice(32, 4));
                var pk = CalculateFromSeed(seed);
                var ivList = pk.IVs;
                (ivList[5], ivList[3], ivList[4]) = (ivList[3], ivList[4], ivList[5]);
                pk.IVs = ivList;
                if (pk.IVs.SequenceEqual(IVFilters))
                {
                    SendNotifications(textBox1.Text, string.Empty);
                    await SaveGame(token).ConfigureAwait(false);
                    MessageBox.Show("Match found!");
                    return;
                }
                if (HardStopEventScan.Checked)
                {
                    MessageBox.Show("HardStop enabled, ending task. Uncheck if you wish to scan until match is found.");
                    break;
                }
                await RolloverCorrectionSV(token).ConfigureAwait(false);
                await Task.Delay(2_000, token).ConfigureAwait(false);
            }
        }

        private async Task SearchForOutbreak(CancellationToken token)
        {
            Settings.Default.OutbreakSpecies = StopOnSpecies.Text;
            Settings.Default.MiscOvershoot = OverShoot.Value;
            Settings.Default.Save();

            var text = StopOnSpecies.Text.Replace(" ", "");
            string[] monlist = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Label[] list = { Ob1Results, Ob2Results, Ob3Results, Ob4Results, Ob5Results, Ob6Results, Ob7Results, Ob8Results };
            PictureBox[] spritelist = { OBSprite1, OBSprite2, OBSprite3, OBSprite4, OBSprite5, OBSprite6, OBSprite7, OBSprite8 };
            List<Image> imagestrings = new();
            List<byte[]?> POSlist = new();
            List<string> strings = new();
            List<PK9> mons = new();
            OutbreakScan.Enabled = false;
            StopOnSpecies.Enabled = false;
            OutbreakSearch.Enabled = false;
            CenterPOS = Array.Empty<byte>();
            while (!token.IsCancellationRequested)
            {
                if (HardStopOutbreak.Checked)
                {
                    MessageBox.Show("HardStop enabled, ending task. Uncheck if you wish to scan until match is found.");
                    break;
                }

                for (int i = 0; i < list.Length; i++)
                {
                    spritelist[i].Image = null;
                    list[i].Text = string.Empty;
                }
                TotalOutbreaks.Text = string.Empty;

                OutbreakScan.Text = "Saving...";
                await SVSaveGameOverworld(token).ConfigureAwait(false);
                OutbreakScan.Text = "Scanning...";
                var validOutbreaks = await ReadEncryptedBlockByte(Blocks.KMassOutbreakTotalEnabled, token).ConfigureAwait(false);
                var Outbreaktotal = Convert.ToInt32(validOutbreaks);
                var block = Blocks.KOutbreakSpecies1;
                var koblock = Blocks.KMassOutbreakKO1;
                var totalblock = Blocks.KMassOutbreak01TotalSpawns;
                var formblock = Blocks.KMassOutbreak01Form;
                var pos = Blocks.KMassOutbreak01CenterPos;
                for (int i = 0; i < 8; i++)
                {
                    TotalOutbreaks.Text = $"Total Outbreaks: {12.5 * i + 1}%";
                    switch (i)
                    {
                        case 0: break;
                        case 1: block = Blocks.KOutbreakSpecies2; formblock = Blocks.KMassOutbreak02Form; koblock = Blocks.KMassOutbreakKO2; totalblock = Blocks.KMassOutbreak02TotalSpawns; pos = Blocks.KMassOutbreak02CenterPos; break;
                        case 2: block = Blocks.KOutbreakSpecies3; formblock = Blocks.KMassOutbreak03Form; koblock = Blocks.KMassOutbreakKO3; totalblock = Blocks.KMassOutbreak03TotalSpawns; pos = Blocks.KMassOutbreak03CenterPos; break;
                        case 3: block = Blocks.KOutbreakSpecies4; formblock = Blocks.KMassOutbreak04Form; koblock = Blocks.KMassOutbreakKO4; totalblock = Blocks.KMassOutbreak04TotalSpawns; pos = Blocks.KMassOutbreak04CenterPos; break;
                        case 4: block = Blocks.KOutbreakSpecies5; formblock = Blocks.KMassOutbreak05Form; koblock = Blocks.KMassOutbreakKO5; totalblock = Blocks.KMassOutbreak05TotalSpawns; pos = Blocks.KMassOutbreak05CenterPos; break;
                        case 5: block = Blocks.KOutbreakSpecies6; formblock = Blocks.KMassOutbreak06Form; koblock = Blocks.KMassOutbreakKO6; totalblock = Blocks.KMassOutbreak06TotalSpawns; pos = Blocks.KMassOutbreak06CenterPos; break;
                        case 6: block = Blocks.KOutbreakSpecies7; formblock = Blocks.KMassOutbreak07Form; koblock = Blocks.KMassOutbreakKO7; totalblock = Blocks.KMassOutbreak07TotalSpawns; pos = Blocks.KMassOutbreak07CenterPos; break;
                        case 7: block = Blocks.KOutbreakSpecies8; formblock = Blocks.KMassOutbreak08Form; koblock = Blocks.KMassOutbreakKO8; totalblock = Blocks.KMassOutbreak08TotalSpawns; pos = Blocks.KMassOutbreak08CenterPos; break;
                    }
                    if (i > Outbreaktotal - 1)
                        continue;

                    var kocount = await ReadEncryptedBlockUint(koblock, token).ConfigureAwait(false);
                    var totalcount = await ReadEncryptedBlockUint(totalblock, token).ConfigureAwait(false);
                    var form = await ReadEncryptedBlockByte(formblock, token).ConfigureAwait(false);
                    var obpos = await ReadEncryptedBlockArray(pos, token).ConfigureAwait(false);
                    PK9 pk = new()
                    {
                        Species = SpeciesConverter.GetNational9((ushort)await ReadEncryptedBlockUint(block, token).ConfigureAwait(false)),
                        Form = form,
                    };
                    CommonEdits.SetIsShiny(pk, false);
                    string pkform = form is 0 ? "" : $"-{form}";
                    strings.Add($"{(Species)pk.Species}{pkform}{Environment.NewLine}Count: {kocount}/{totalcount}");
                    var img = SpriteUtil.SB8a.GetSprite(pk.Species, pk.Form, 0, 0, 0, false, Shiny.Never, EntityContext.Gen9);
                    imagestrings.Add(img);
                    mons.Add(pk);
                    POSlist.Add(obpos);
                }

                TotalOutbreaks.Text = $"Total Outbreaks: {Outbreaktotal}";

                for (int i = 0; i < imagestrings.Count; i++)
                {
                    spritelist[i].Image = imagestrings[i];
                    list[i].Text = strings[i].ToString();
                }

                for (int i = 0; i < mons.Count; i++)
                {
                    bool huntedspecies = monlist.Contains($"{(Species)mons[i].Species}");
                    if (huntedspecies && monlist.Length != 0 && OutbreakSearch.Checked)
                    {
                        CollideButton.Enabled = true;
                        var mapsprite = PokeImg(mons[i], false);
                        HttpClient client = new();
                        using (Stream stream = await client.GetStreamAsync(mapsprite, token))
                        {
                            MapSprite = Image.FromStream(stream);
                        }
                        CenterPOS = POSlist[i];
                        MapViewSV form = new(MapSprite, CenterPOS);
                        await Task.Run(() => { form.ShowDialog(); }, token);

                        string msg = $"{(Species)mons[i].Species} outbreak found!";
                        if (EnableWebhook.Checked)
                        {
                            var sprite = PokeImg(mons[i], false);
                            SendNotifications(msg, sprite);
                        }
                        else
                            MessageBox.Show(msg);

                        EnableAssets();                        
                        return;

                    }
                }

                imagestrings = new();
                POSlist = new();
                strings = new();
                mons = new();

                if (HardStopOutbreak.Checked)
                {
                    MessageBox.Show("HardStop enabled, ending task. Uncheck if you wish to scan until match is found.");
                    {
                        EnableAssets();
                        return;
                    }
                }
                if (OutbreakSearch.Checked)
                {
                    OutbreakScan.Text = "Skipping...";
                    await RolloverCorrectionSV(token).ConfigureAwait(false);
                    await Task.Delay(2_000, token).ConfigureAwait(false);
                }
                else if (!OutbreakSearch.Checked)
                    break;
            }
            EnableAssets();
        }

        private async void KOToSixty_Click(object sender, EventArgs e)
        {
            var token = CancellationToken.None;
            var validOutbreaks = await ReadEncryptedBlockByte(Blocks.KMassOutbreakTotalEnabled, token).ConfigureAwait(false);
            var Outbreaktotal = Convert.ToInt32(validOutbreaks);
            var koblock = Blocks.KMassOutbreakKO1;
            for (int i = 0; i < 8; i++)
            {
                TotalOutbreaks.Text = $"Total Outbreaks: {12.5 * i + 1}%";
                switch (i)
                {
                    case 0: break;
                    case 1: koblock = Blocks.KMassOutbreakKO2; break;
                    case 2: koblock = Blocks.KMassOutbreakKO3; break;
                    case 3: koblock = Blocks.KMassOutbreakKO4; break;
                    case 4: koblock = Blocks.KMassOutbreakKO5; break;
                    case 5: koblock = Blocks.KMassOutbreakKO6; break;
                    case 6: koblock = Blocks.KMassOutbreakKO7; break;
                    case 7: koblock = Blocks.KMassOutbreakKO8; break;
                }
                if (i > Outbreaktotal - 1)
                    continue;

                var currentcount = (uint)await ReadEncryptedBlockInt32(koblock, token).ConfigureAwait(false);
                uint inj = 61;
                await WriteBlock(inj, koblock, token, currentcount).ConfigureAwait(false);
            }
            TotalOutbreaks.Text = "Done.";
            await SearchForOutbreak(token).ConfigureAwait(false);
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            await SearchForOutbreak(CancellationToken.None).ConfigureAwait(false);
        }

        private async void CollideButton_Click(object sender, EventArgs e)
        {
            if (CenterPOS is not null)
            {
                float Y = BitConverter.ToSingle(CenterPOS, 4);
                Y += 40;
                WriteSingleLittleEndian(CenterPOS.AsSpan()[4..], Y);

                for (int i = 0; i < 15; i++)
                    await Executor.SwitchConnection.PointerPoke(CenterPOS, CollisionPointer, CancellationToken.None).ConfigureAwait(false);
            }

            if (CenterPOS is null)
            {
                MessageBox.Show("No valid coordinates present. Try again after finding a desired outbreak.");
                CollideButton.Enabled = false;
            }

        }

        private async void button1_Click_1(object sender, EventArgs e)
        {
            var token = CancellationToken.None;
            var vivform = await ReadEncryptedBlockByte(Blocks.KGOVivillonForm, token).ConfigureAwait(false);
            var epochtime = await ReadEncryptedBlockUint(Blocks.KGOLastConnected, token).ConfigureAwait(false);

            var inj = (byte)V_Form;
            if (inj != vivform)
                await WriteBlock(inj, Blocks.KGOVivillonForm, token, vivform).ConfigureAwait(false);

            var newform = await ReadEncryptedBlockByte(Blocks.KGOVivillonForm, token).ConfigureAwait(false);

            TimeSpan t = DateTime.Now - new DateTime(1970, 1, 1);
            uint secondsSinceEpoch = (uint)t.TotalSeconds;

            var inj2 = (uint)EpochNumeric.Value;
            await WriteBlock((inj2 * 86400) + secondsSinceEpoch, Blocks.KGOLastConnected, token, epochtime).ConfigureAwait(false);

            var newtime = await ReadEncryptedBlockUint(Blocks.KGOLastConnected, token).ConfigureAwait(false);

            await WriteBlock(true, Blocks.KGOVivillonFormEnabled, token, false).ConfigureAwait(false);

            string vivmsg = inj != vivform ? $"Vivillon form has been changed from {(VivForms)vivform} to {(VivForms)newform}." : "Modified form is the same as the current form.";
            MessageBox.Show($"{vivmsg}{Environment.NewLine}KGOLastConnected TimeStamp changed from {FromUnixTime(epochtime)} to {FromUnixTime(newtime)}");
        }

        private async void ReadValues_Click(object sender, EventArgs e)
        {
            var token = CancellationToken.None;
            var forcevivform = await ReadEncryptedBlockBool(Blocks.KGOVivillonFormEnabled, token).ConfigureAwait(false);
            var vivform = await ReadEncryptedBlockByte(Blocks.KGOVivillonForm, token).ConfigureAwait(false);
            var epochtime = await ReadEncryptedBlockUint(Blocks.KGOLastConnected, token).ConfigureAwait(false);

            MessageBox.Show($"KGOVivillonFormEnabled: {forcevivform}{Environment.NewLine}" +
                $"KGOVivillonForm: {(VivForms)vivform}{Environment.NewLine}KGOVivillon Form TimeStamp on {FromUnixTime(epochtime)}");
        }

        public class DataBlock
        {
            public string? Name { get; set; }
            public uint Key { get; set; }
            public SCTypeCode Type { get; set; }
            public SCTypeCode SubType { get; set; }
            public IReadOnlyList<long>? Pointer { get; set; }
            public bool IsEncrypted { get; set; }
            public int Size { get; set; }
        }

        public static class Blocks
        {
            public static DataBlock KMassOutbreakTotalEnabled = new()
            {
                Name = "KMassOutbreakTotalEnabled",
                Key = 0x6C375C8A,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x13A80 },
                IsEncrypted = true,
                Size = 1,
            };
            #region Outbreak1
            public static DataBlock KOutbreakSpecies1 = new()
            {
                Name = "KOutbreakSpecies1",
                Key = 0x76A2F996,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15B80 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak01Form = new()
            {
                Name = "KMassOutbreak01Form",
                Key = 0x29B4615D,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x7840 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO1 = new()
            {
                Name = "KMassOutbreak01NumKOed",
                Key = 0x4B16FBC2,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDAE0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak01TotalSpawns = new()
            {
                Name = "KMassOutbreak01TotalSpawns",
                Key = 0xB7DC495A,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x22920 },
            };
            public static DataBlock KMassOutbreak01CenterPos = new()
            {
                Name = "KMassOutbreak01CenterPos",
                Key = 0x2ED42F4D,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x8800 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Outbreak2
            public static DataBlock KOutbreakSpecies2 = new()
            {
                Name = "KOutbreakSpecies2",
                Key = 0x76A0BCF3,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15B60 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak02Form = new()
            {
                Name = "KMassOutbreak02Form",
                Key = 0x29B84368,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x7880 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO2 = new()
            {
                Name = "KMassOutbreak02NumKOed",
                Key = 0x4B14BF1F,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDAA0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak02TotalSpawns = new()
            {
                Name = "KMassOutbreak02TotalSpawns",
                Key = 0xB7DA0CB7,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x228E0 },
            };
            public static DataBlock KMassOutbreak02CenterPos = new()
            {
                Name = "KMassOutbreak02CenterPos",
                Key = 0x2ED5F198,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x8820 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Outbreak3
            public static DataBlock KOutbreakSpecies3 = new()
            {
                Name = "KOutbreakSpecies3",
                Key = 0x76A97E38,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15BC0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak03Form = new()
            {
                Name = "KMassOutbreak03Form",
                Key = 0x29AF8223,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x7800 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO3 = new()
            {
                Name = "KMassOutbreak03NumKOed",
                Key = 0x4B1CA6E4,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDB40 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak03TotalSpawns = new()
            {
                Name = "KMassOutbreak03TotalSpawns",
                Key = 0xB7E1F47C,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x22960 },
            };
            public static DataBlock KMassOutbreak03CenterPos = new()
            {
                Name = "KMassOutbreak03CenterPos",
                Key = 0x2ECE09D3,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x87C0 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Outbreak4
            public static DataBlock KOutbreakSpecies4 = new()
            {
                Name = "KOutbreakSpecies4",
                Key = 0x76A6E26D,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15BA0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak04Form = new()
            {
                Name = "KMassOutbreak04Form",
                Key = 0x29B22B86,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x7820 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO4 = new()
            {
                Name = "KMassOutbreak04NumKOed",
                Key = 0x4B1A77D9,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDB20 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak04TotalSpawns = new()
            {
                Name = "KMassOutbreak04TotalSpawns",
                Key = 0xB7DFC571,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x22940 },
            };
            public static DataBlock KMassOutbreak04CenterPos = new()
            {
                Name = "KMassOutbreak04CenterPos",
                Key = 0x2ED04676,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x87E0 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Outbreak5
            public static DataBlock KOutbreakSpecies5 = new()
            {
                Name = "KOutbreakSpecies5",
                Key = 0x76986F3A,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15B00 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak05Form = new()
            {
                Name = "KMassOutbreak05Form",
                Key = 0x29A9D701,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x77C0 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO5 = new()
            {
                Name = "KMassOutbreak05NumKOed",
                Key = 0x4B23391E,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDBA0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak05TotalSpawns = new()
            {
                Name = "KMassOutbreak05TotalSpawns",
                Key = 0xB7E886B6,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x229C0 },
            };
            public static DataBlock KMassOutbreak05CenterPos = new()
            {
                Name = "KMassOutbreak05CenterPos",
                Key = 0x2EC78531,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x8780 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Outbreak6
            public static DataBlock KOutbreakSpecies6 = new()
            {
                Name = "KOutbreakSpecies6",
                Key = 0x76947F97,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15AC0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak06Form = new()
            {
                Name = "KMassOutbreak06Form",
                Key = 0x29AB994C,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x77E0 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO6 = new()
            {
                Name = "KMassOutbreak06NumKOed",
                Key = 0x4B208FBB,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDB60 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak06TotalSpawns = new()
            {
                Name = "KMassOutbreak06TotalSpawns",
                Key = 0xB7E49713,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x229A0 },
            };
            public static DataBlock KMassOutbreak06CenterPos = new()
            {
                Name = "KMassOutbreak06CenterPos",
                Key = 0x2ECB673C,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x87A0 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Outbreak7
            public static DataBlock KOutbreakSpecies7 = new()
            {
                Name = "KOutbreakSpecies7",
                Key = 0x769D40DC,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15B40 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak07Form = new()
            {
                Name = "KMassOutbreak07Form",
                Key = 0x29A344C7,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x7740 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO7 = new()
            {
                Name = "KMassOutbreak07NumKOed",
                Key = 0x4B28E440,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDBE0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak07TotalSpawns = new()
            {
                Name = "KMassOutbreak07TotalSpawns",
                Key = 0xB7EE31D8,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x22A00 },
            };
            public static DataBlock KMassOutbreak07CenterPos = new()
            {
                Name = "KMassOutbreak07CenterPos",
                Key = 0x2EC1CC77,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x8740 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Outbreak8
            public static DataBlock KOutbreakSpecies8 = new()
            {
                Name = "KOutbreakSpecies8",
                Key = 0x769B11D1,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x15B20 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak08Form = new()
            {
                Name = "KMassOutbreak08Form",
                Key = 0x29A5EE2A,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x77A0 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KMassOutbreakKO8 = new()
            {
                Name = "KMassOutbreak08NumKOed",
                Key = 0x4B256EF5,
                Type = SCTypeCode.UInt32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xDBC0 },
                IsEncrypted = true,
                Size = 4,
            };
            public static DataBlock KMassOutbreak08TotalSpawns = new()
            {
                Name = "KMassOutbreak08TotalSpawns",
                Key = 0xB7EABC8D,
                Type = SCTypeCode.Int32,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x229E0 },
            };
            public static DataBlock KMassOutbreak08CenterPos = new()
            {
                Name = "KMassOutbreak08CenterPos",
                Key = 0x2EC5BC1A,
                Type = SCTypeCode.Array,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x8760 },
                IsEncrypted = true,
                Size = 12,
            };
            #endregion
            #region Vivillon
            public static DataBlock KGOVivillonFormEnabled = new()
            {
                Name = "KGOVivillonFormEnabled",
                Key = 0x0C125D5C,
                Type = SCTypeCode.Bool1,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x2020 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KGOTransfer = new()
            {
                Name = "KGOTransfer",
                Key = 0x7EE0A576,
                Type = SCTypeCode.Object,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x17320 },
                IsEncrypted = true,
                Size = 0x3400,
            };
            public static DataBlock FSYS_GO_LINK_ENABLED = new()
            {
                Name = "FSYS_GO_LINK_ENABLED",
                Key = 0x3ABC21E3,
                Type = SCTypeCode.Bool1,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0xAA60 },
                IsEncrypted = true,
                Size = 1,
            };

            public static DataBlock KGOVivillonForm = new()
            {
                Name = "KGOVivillonForm",
                Key = 0x22F70BCF,
                Type = SCTypeCode.Byte,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x6440 },
                IsEncrypted = true,
                Size = 1,
            };
            public static DataBlock KGOLastConnected = new()
            {
                Name = "KGOLastConnected",
                Key = 0x867F0240,
                Type = SCTypeCode.UInt64,
                Pointer = new long[] { 0x449EEE8, 0xD8, 0x0, 0x0, 0x30, 0x8, 0x188E0 },
                IsEncrypted = true,
                Size = 8,
            };
            #endregion
        }

        private async Task SaveGame(CancellationToken token)
        {
            await Click(X, 3_000, token).ConfigureAwait(false);
            await Click(R, 1_500, token).ConfigureAwait(false);
            await Click(A, 3_000, token).ConfigureAwait(false);
            await Click(A, 2_000, token).ConfigureAwait(false);
        }
        public new async Task Click(SwitchButton b, int delay, CancellationToken token)
        {
            await Executor.Connection.SendAsync(SwitchCommand.Click(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        public async Task PressAndHold(SwitchButton b, int hold, int delay, CancellationToken token)
        {
            await Executor.Connection.SendAsync(SwitchCommand.Hold(b, true), token).ConfigureAwait(false);
            await Task.Delay(hold, token).ConfigureAwait(false);
            await Executor.Connection.SendAsync(SwitchCommand.Release(b, true), token).ConfigureAwait(false);
            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task RolloverCorrectionSV(CancellationToken token)
        {
            await Task.Delay(0_050, token).ConfigureAwait(false);
            await Click(HOME, 2_000, token).ConfigureAwait(false); // Back to title screen

            for (int i = 0; i < 2; i++)
                await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(A, 1_250, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            await PressAndHold(DDOWN, (int)OverShoot.Value, 1_000, token).ConfigureAwait(false);
            await Click(DUP, 0_500, token).ConfigureAwait(false);

            await Click(A, 1_250, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(DDOWN, 0_250, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);

            for (int i = 0; i < 8; i++) // Mash DRIGHT to confirm
                await Click(DRIGHT, 0_200, token).ConfigureAwait(false);

            await Click(A, 0_200, token).ConfigureAwait(false); // Confirm date/time change
            await Click(HOME, 1_000, token).ConfigureAwait(false);
            await Click(A, 4_000, token).ConfigureAwait(false); // Back to title screen
        }

        // Via Manu's SV Research
        private const ushort UserTID = 12345;
        private const ushort UserSID = 54321;
        private const int UNSET = -1;

        private static bool IsShiny(uint PID, ushort TID = UserTID, ushort SID = UserSID) =>
        (ushort)(SID ^ TID ^ (PID >> 16) ^ PID) < 16;

        private static bool IsShiny(uint PID, uint FTID) =>
            IsShiny(PID, (ushort)(FTID >> 16), (ushort)(FTID & 0xFFFF));

        private static uint ForceShiny(uint PID, uint TID = UserTID, uint SID = UserSID) =>
            ((TID ^ SID ^ (PID & 0xFFFF) ^ 1) << 16) | (PID & 0xFFFF);

        private static uint ForceNonShiny(uint PID) => PID ^ 0x10000000;

        private PK9 CalculateFromSeed(uint seed)
        {
            PK9 pk = new();
            int i;
            var xoro = new Xoroshiro128Plus(seed);
            pk.EncryptionConstant = (uint)xoro.Next();
            var fakeTrainer = (uint)xoro.Next();

            pk.PID = 0;
            var isShiny = false;
            for (i = 0; i < 1; i++)
            {
                pk.PID = (uint)xoro.Next();
                isShiny = IsShiny(pk.PID, fakeTrainer);
                if (isShiny)
                {
                    if (!IsShiny(pk.PID))
                        pk.PID = ForceShiny(pk.PID);
                    break;
                }
                else
                    if (IsShiny(pk.PID))
                    pk.PID = ForceNonShiny(pk.PID);
            }


            int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
            var determined = 0;
            while (determined < 4)
            {
                var idx = xoro.NextInt(6);
                if (ivs[idx] != UNSET)
                    continue;
                ivs[idx] = 31;
                determined++;
            }

            for (i = 0; i < ivs.Length; i++)
                if (ivs[i] == UNSET)
                    ivs[i] = (int)xoro.NextInt(31 + 1);

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            textBox1.Text = $"Seed: 0x{seed:X8}{Environment.NewLine}" +
                $"EC: {pk.EncryptionConstant:X8}{Environment.NewLine}" +
                $"PID: {pk.PID:X8}{Environment.NewLine}" +
                $"IVs: {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}";
            return pk;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using StopConditions miniform = new();
            miniform.ShowDialog();
        }

        private static int[] GrabIvFilters()
        {
            int[] ivsequence = Array.Empty<int>();
            int filters = Settings.Default.PresetIVS;
            switch (filters)
            {
                case 0: ivsequence = new[] { Settings.Default.HPFilter, Settings.Default.AtkFilter, Settings.Default.DefFilter, Settings.Default.SpaFilter, Settings.Default.SpdFilter, Settings.Default.SpeFilter }; break;
                case 1: ivsequence = new[] { 31, 31, 31, 31, 31, 31 }; break;
                case 2: ivsequence = new[] { 31, 0, 31, 31, 31, 0 }; break;
                case 3: ivsequence = new[] { 31, 0, 31, 31, 31, 31 }; break;
                case 4: ivsequence = new[] { 31, 31, 31, 31, 31, 0 }; break;
            }
            return ivsequence;
        }

        private static HttpClient? _client;
        private static HttpClient Client
        {
            get
            {
                _client ??= new HttpClient();
                return _client;
            }
        }

        private static string[]? DiscordWebhooks;

        private async void SendNotifications(string results, string thumbnail)
        {
            if (string.IsNullOrEmpty(results) || string.IsNullOrEmpty(Settings.Default.WebHook))
                return;
            DiscordWebhooks = Settings.Default.WebHook.Split(',');
            if (DiscordWebhooks == null)
                return;
            var webhook = GenerateWebhook(results, thumbnail);
            var content = new StringContent(JsonConvert.SerializeObject(webhook), Encoding.UTF8, "application/json");
            foreach (var url in DiscordWebhooks)
                await Client.PostAsync(url, content).ConfigureAwait(false);
        }

        private static object GenerateWebhook(string results, string thumbnail)
        {
            var WebHook = new
            {
                username = $"PokeViewer.NET",
                content = $"<@{Settings.Default.UserDiscordID}>",
                embeds = new List<object>
                {
                    new
                    {
                        title = $"Match Found!",
                        thumbnail = new
                        {
                            url = thumbnail
                        },
                        fields = new List<object>
                        {
                            new { name = "Description               ", value = results, inline = true, },
                        },
                    }
                }
            };
            return WebHook;
        }

        // Read, Decrypt, and Write Block tasks from Tera-Finder/RaidCrawler/sv-livemap.
        #region saveblocktasks
        public static byte[] DecryptBlock(uint key, byte[] block)
        {
            var rng = new SCXorShift32(key);
            for (int i = 0; i < block.Length; i++)
                block[i] = (byte)(block[i] ^ rng.Next());
            return block;
        }

        private static IEnumerable<long> PreparePointer(IEnumerable<long> pointer)
        {
            var count = pointer.Count();
            var p = new long[count + 1];
            for (var i = 0; i < pointer.Count(); i++)
                p[i] = pointer.ElementAt(i);
            p[count - 1] += 8;
            p[count] = 0x0;
            return p;
        }

        private async Task<ulong> GetBlockAddress(DataBlock block, CancellationToken token)
        {
            var read_key = ReadUInt32LittleEndian(await Executor.SwitchConnection.PointerPeek(4, block.Pointer!, token).ConfigureAwait(false));
            if (read_key == block.Key)
                return await Executor.SwitchConnection.PointerAll(PreparePointer(block.Pointer!), token).ConfigureAwait(false);
            var direction = block.Key > read_key ? 1 : -1;
            var base_offset = block.Pointer![block.Pointer.Count - 1];
            for (var offset = base_offset; offset < base_offset + 0x1000 && offset > base_offset - 0x1000; offset += direction * 0x20)
            {
                var pointer = block.Pointer!.ToArray();
                pointer[^1] = offset;
                read_key = ReadUInt32LittleEndian(await Executor.SwitchConnection.PointerPeek(4, pointer, token).ConfigureAwait(false));
                if (read_key == block.Key)
                    return await Executor.SwitchConnection.PointerAll(PreparePointer(pointer), token).ConfigureAwait(false);
            }
            throw new ArgumentOutOfRangeException("Save block not found in range +- 0x1000. Restart the game and try again.");
        }

        private async Task<byte> ReadEncryptedBlockByte(DataBlock block, CancellationToken token)
        {
            var header = await ReadEncryptedBlockHeader(block, token).ConfigureAwait(false);
            return header[1];
        }

        private async Task<byte[]> ReadEncryptedBlockHeader(DataBlock block, CancellationToken token)
        {
            var address = await GetBlockAddress(block, token).ConfigureAwait(false);
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            return header;
        }

        private async Task<byte[]?> ReadEncryptedBlockArray(DataBlock block, CancellationToken token)
        {

            var address = await GetBlockAddress(block, token).ConfigureAwait(false);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 6 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);
            return data[6..];
        }

        private async Task<uint> ReadEncryptedBlockUint(DataBlock block, CancellationToken token)
        {
            var header = await ReadEncryptedBlockHeader(block, token).ConfigureAwait(false);
            return ReadUInt32LittleEndian(header.AsSpan()[1..]);
        }

        private async Task<int> ReadEncryptedBlockInt32(DataBlock block, CancellationToken token)
        {
            var header = await ReadEncryptedBlockHeader(block, token).ConfigureAwait(false);
            return ReadInt32LittleEndian(header.AsSpan()[1..]);
        }

        private async Task<bool> ReadEncryptedBlockBool(DataBlock block, CancellationToken token)
        {
            var address = await GetBlockAddress(block, token).ConfigureAwait(false);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, block.Size, token).ConfigureAwait(false);
            var res = DecryptBlock(block.Key, data);
            return res[0] == 2;
        }

        private async Task<byte[]> ReadBlock(DataBlock block, CancellationToken token)
        {
            return await ReadEncryptedBlock(block, token).ConfigureAwait(false);
        }

        private async Task<byte[]> ReadEncryptedBlock(DataBlock block, CancellationToken token)
        {
            var address = await GetBlockAddress(block, token).ConfigureAwait(false);
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            var size = ReadUInt32LittleEndian(header.AsSpan()[1..]);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5 + (int)size, token);
            var res = DecryptBlock(block.Key, data)[5..];
            return res;
        }

        private async Task<byte[]?> ReadEncryptedBlockObject(DataBlock block, CancellationToken token)
        {
            var address = await GetBlockAddress(block, token).ConfigureAwait(false);
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            var size = ReadUInt32LittleEndian(header.AsSpan()[1..]);
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5 + (int)size, token);
            var res = DecryptBlock(block.Key, data)[5..];
            return res;
        }

        public async Task<bool> WriteBlock(object data, DataBlock block, CancellationToken token, object? toExpect = default)
        {
            if (block.IsEncrypted)
                return await WriteEncryptedBlockSafe(block, toExpect, data, token).ConfigureAwait(false);
            else
                return await WriteDecryptedBlock((byte[])data!, block, token).ConfigureAwait(false);
        }

        private async Task<bool> WriteDecryptedBlock(byte[] data, DataBlock block, CancellationToken token)
        {
            await Executor.SwitchConnection.PointerPoke(data, block.Pointer!, token).ConfigureAwait(false);

            return true;
        }

        private async Task<bool> WriteEncryptedBlockSafe(DataBlock block, object? toExpect, object toWrite, CancellationToken token)
        {
            if (toExpect == default || toWrite == default)
                return false;

            return block.Type switch
            {
                SCTypeCode.Array => await WriteEncryptedBlockArray(block, (byte[])toExpect, (byte[])toWrite, token).ConfigureAwait(false),
                SCTypeCode.Bool1 or SCTypeCode.Bool2 or SCTypeCode.Bool3 => await WriteEncryptedBlockBool(block, (bool)toExpect, (bool)toWrite, token).ConfigureAwait(false),
                SCTypeCode.Byte or SCTypeCode.SByte => await WriteEncryptedBlockByte(block, (byte)toExpect, (byte)toWrite, token).ConfigureAwait(false),
                SCTypeCode.UInt32 or SCTypeCode.UInt64 => await WriteEncryptedBlockUint(block, (uint)toExpect, (uint)toWrite, token).ConfigureAwait(false),
                SCTypeCode.Int32 => await WriteEncryptedBlockInt32(block, (int)toExpect, (int)toWrite, token).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Block {block.Name} (Type {block.Type}) is currently not supported.")
            };
        }

        private async Task<bool> WriteEncryptedBlockUint(DataBlock block, uint valueToExpect, uint valueToInject, CancellationToken token)
        {
            ulong address;
            try { address = await GetBlockAddress(block, token).ConfigureAwait(false); }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            //Validate ram data
            var ram = ReadUInt32LittleEndian(header.AsSpan()[1..]);
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            WriteUInt32LittleEndian(header.AsSpan()[1..], valueToInject);
            header = EncryptBlock(block.Key, header);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(header, address, token).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> WriteEncryptedBlockInt32(DataBlock block, int valueToExpect, int valueToInject, CancellationToken token)
        {
            ulong address;
            try { address = await GetBlockAddress(block, token).ConfigureAwait(false); }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            //Validate ram data
            var ram = ReadInt32LittleEndian(header.AsSpan()[1..]);
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            WriteInt32LittleEndian(header.AsSpan()[1..], valueToInject);
            header = EncryptBlock(block.Key, header);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(header, address, token).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> WriteEncryptedBlockByte(DataBlock block, byte valueToExpect, byte valueToInject, CancellationToken token)
        {
            ulong address;
            try { address = await GetBlockAddress(block, token).ConfigureAwait(false); }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var header = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            //Validate ram data
            var ram = header[1];
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            header[1] = valueToInject;
            header = EncryptBlock(block.Key, header);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(header, address, token).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> WriteEncryptedBlockArray(DataBlock block, byte[] arrayToExpect, byte[] arrayToInject, CancellationToken token)
        {
            ulong address;
            try { address = await GetBlockAddress(block, token).ConfigureAwait(false); }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, 6 + block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);
            //Validate ram data
            var ram = data[6..];
            if (!ram.SequenceEqual(arrayToExpect)) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            Array.ConstrainedCopy(arrayToInject, 0, data, 6, block.Size);
            data = EncryptBlock(block.Key, data);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> WriteEncryptedBlockBool(DataBlock block, bool valueToExpect, bool valueToInject, CancellationToken token)
        {
            ulong address;
            try { address = await GetBlockAddress(block, token).ConfigureAwait(false); }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var data = await Executor.SwitchConnection.ReadBytesAbsoluteAsync(address, block.Size, token).ConfigureAwait(false);
            data = DecryptBlock(block.Key, data);
            //Validate ram data
            var ram = data[0] == 2;
            if (ram != valueToExpect) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            data[0] = valueToInject ? (byte)2 : (byte)1;
            data = EncryptBlock(block.Key, data);
            await Executor.SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);
            return true;
        }

        public static byte[] EncryptBlock(uint key, byte[] block) => DecryptBlock(key, block);
        #endregion

        private async Task SVSaveGameOverworld(CancellationToken token)
        {
            await Click(X, 2_000, token).ConfigureAwait(false);
            await Click(R, 1_800, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 5_000, token).ConfigureAwait(false);
        }

        private void EnableAssets()
        {
            OutbreakScan.Enabled = true;
            OutbreakScan.Text = "Outbreak Scan";
            OutbreakSearch.Enabled = true;
            StopOnSpecies.Enabled = true;
        }

        private IReadOnlyList<long> CollisionPointer { get; } = new long[] { 0x44CCA90, 0xAD8, 0x160, 0x60, 0x100 };

        private static readonly DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);

        public static DateTime FromUnixTime(long unixTime)
        {
            return epoch.AddSeconds(unixTime);
        }

        private void V_ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selection = V_ComboBox.SelectedIndex;
            switch (selection)
            {
                case 0: V_Form = (int)VivForms.IcySnow; break; // Icy Snow
                case 1: V_Form = (int)VivForms.Polar; break; // Polar
                case 2: V_Form = (int)VivForms.Tundra; break; // Tundra
                case 3: V_Form = (int)VivForms.Continental; break; // Continental
                case 4: V_Form = (int)VivForms.Garden; break; // Garden
                case 5: V_Form = (int)VivForms.Elegant; break; // Elegant
                case 6: V_Form = (int)VivForms.Meadow; break; // Meadow
                case 7: V_Form = (int)VivForms.Modern; break; // Modern
                case 8: V_Form = (int)VivForms.Marine; break; // Marine
                case 9: V_Form = (int)VivForms.Archipelago; break; // Archipelago
                case 10: V_Form = (int)VivForms.HighPlains; break; // High-Plains
                case 11: V_Form = (int)VivForms.Sandstorm; break;// Sandstorm
                case 12: V_Form = (int)VivForms.River; break; // River
                case 13: V_Form = (int)VivForms.Monsoon; break; // Monsoon
                case 14: V_Form = (int)VivForms.Savanna; break; // Savanna
                case 15: V_Form = (int)VivForms.Sun; break; // Sun
                case 16: V_Form = (int)VivForms.Ocean; break; // Ocean
                case 17: V_Form = (int)VivForms.Jungle; break; // Jungle
                case 18: V_Form = (int)VivForms.Fancy; break; // Fancy
            }
        }

        private enum VivForms
        {
            IcySnow = 0,
            Polar = 1,
            Tundra = 2,
            Continental = 3,
            Garden = 4,
            Elegant = 5,
            Meadow = 6,
            Modern = 7,
            Marine = 8,
            Archipelago = 9,
            HighPlains = 10,
            Sandstorm = 11,
            River = 12,
            Monsoon = 13,
            Savanna = 14,
            Sun = 15,
            Ocean = 16,
            Jungle = 17,
            Fancy = 18,
        }

    }
}
