﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Core
{
    public class ClsVgm
    {

        public Conductor[] conductor = null;
        public YM2612[] ym2612 = null;
        public SN76489[] sn76489 = null;

        public Dictionary<enmChipType, ClsChip[]> chips;

        public int lineNumber = 0;

        public Dictionary<int, mucomVoice> instFM = new Dictionary<int, mucomVoice>();
        public Dictionary<int, int[]> instENV = new Dictionary<int, int[]>();
        public Dictionary<int, clsPcm> instPCM = new Dictionary<int, clsPcm>();
        public Dictionary<int, Ssgdat.Instrument> instSSG = new Dictionary<int, Ssgdat.Instrument>();
        public List<clsPcmDatSeq> instPCMDatSeq = new List<clsPcmDatSeq>();
        public Dictionary<int, clsToneDoubler> instToneDoubler = new Dictionary<int, clsToneDoubler>();
        public Dictionary<int, byte[]> instWF = new Dictionary<int, byte[]>();

        public Dictionary<string, List<Line>> partData = new Dictionary<string, List<Line>>();
        public Dictionary<int, Line> aliesData = new Dictionary<int, Line>();

        private int instrumentCounter = -1;
        private byte[] instrumentBufCache = new byte[Const.INSTRUMENT_SIZE];
        private int toneDoublerCounter = -1;
        private List<int> toneDoublerBufCache = new List<int>();
        private int wfInstrumentCounter = -1;
        private byte[] wfInstrumentBufCache = null;

        public int newStreamID = -1;

        public Information info = null;
        public int useJumpCommand = 0;
        public bool PCMmode = false;

        private char[] PART_OPN2 = new char[] { 'A', 'B', 'C', 'G', 'H', 'I', 'K' };
        private char[] PART_DCSG = new char[] { 'D', 'E', 'F', 'J' };
        private string stPath;
        private string srcFn;

        public ClsVgm(string stPath,string srcFn)
        {
            this.stPath = stPath;
            this.srcFn = srcFn;

            chips = new Dictionary<enmChipType, ClsChip[]>();
            info = new Information();

            conductor = new Conductor[] { new Conductor(this, 0, "Cn", stPath, false) };
            ym2612 = new YM2612[] { new YM2612(this, 0, "F", stPath, false) };
            sn76489 = new SN76489[] { new SN76489(this, 0, "S", stPath, false)};

            chips.Add(enmChipType.CONDUCTOR, conductor);
            chips.Add(enmChipType.YM2612, ym2612);
            chips.Add(enmChipType.SN76489, sn76489);

            List<clsTD> lstTD = new List<clsTD>
            {
                new clsTD(4, 4, 4, 4, 0, 0, 0, 0, 0),
                new clsTD(3, 3, 4, 4, 1, 1, 0, 0, 0),
                new clsTD(4, 4, 5, 5, 1, 1, 0, 0, -4),
                new clsTD(3, 3, 4, 4, 2, 2, 0, 0, 0),
                new clsTD(5, 5, 4, 4, 0, 0, 0, 0, 0),
                new clsTD(4, 4, 3, 3, 0, 0, 0, 0, 5),
                new clsTD(4, 4, 4, 4, 1, 1, 0, 0, 0),
                new clsTD(6, 6, 4, 4, 0, 0, 0, 0, 0),
                new clsTD(4, 4, 4, 4, 2, 2, 0, 0, 0),
                new clsTD(6, 6, 5, 5, 1, 1, 0, 0, -4),
                new clsTD(5, 5, 4, 4, 1, 1, 0, 0, 0),
                new clsTD(6, 6, 5, 5, 2, 2, 0, 0, -4),
                new clsTD(8, 8, 4, 4, 0, 0, 0, 0, 0),
                new clsTD(6, 6, 4, 4, 1, 1, 0, 0, 0),
                new clsTD(8, 8, 5, 5, 1, 1, 0, 0, -4),
                new clsTD(6, 6, 4, 4, 2, 2, 0, 0, 0),
                new clsTD(10, 10, 4, 4, 0, 0, 0, 0, 0),
                new clsTD(8, 8, 3, 3, 0, 0, 0, 0, 5),
                new clsTD(8, 8, 4, 4, 1, 1, 0, 0, 0),
                new clsTD(12, 12, 4, 4, 0, 0, 0, 0, 0)
            };
            clsToneDoubler toneDoubler = new clsToneDoubler(0, lstTD);
            instToneDoubler.Add(0, toneDoubler);

        }

        public void LoadVoicedat()
        {
            //mucファイルのある位置にあるfn
            string mucPathVoice = Path.Combine(Path.GetDirectoryName(srcFn), info.Voice);

            //mdplayerがある位置にあるfn
            string mdpPathVoice = Path.Combine(stPath, "voice.dat");
            string decideVoice = "";

            if (!File.Exists(mucPathVoice))
            {
                if (!File.Exists(mdpPathVoice))
                {
                    return;
                }
                decideVoice = mdpPathVoice;
            }
            else
            {
                decideVoice = mucPathVoice;
            }

            byte[] voidat = File.ReadAllBytes(decideVoice);
            int No = 0;
            while (No * 32 < voidat.Length)
            {
                mucomVoice voi = new mucomVoice();
                voi.No = No;
                voi.type = 1;//%
                voi.Name = Encoding.GetEncoding("Shift_JIS").GetString(voidat, No * 32 + 0x1a, 6);
                voi.data = new byte[25];
                Array.Copy(voidat, No * 32 + 1, voi.data, 0, 25);
                if (!instFM.ContainsKey(No))
                {
                    instFM.Add(No , voi);
                }
                else
                {
                    instFM[No].Name = voi.Name;
                }
                No++;
            }

        }

        public void LoadSSGdat()
        {
            //mucomMD2vgmがある位置にあるfn
            string fnSsg = Path.Combine(stPath, "ssgdat.xml");
            Ssgdat Ssgdat= Ssgdat.Load(fnSsg);
            instSSG = new Dictionary<int, Ssgdat.Instrument>();
            foreach(Ssgdat.Instrument ins in Ssgdat.Instruments)
            {
                if (instSSG.ContainsKey(ins.No))
                {
                    instSSG.Remove(ins.No);
                }
                instSSG.Add(ins.No, ins);
            }
        }
        
        public void LoadAdpcmdat()
        {
            //mucファイルのある位置にあるfn
            string mucPathPcm = Path.Combine(Path.GetDirectoryName(srcFn), info.Pcm);

            //mdplayerがある位置にあるfn
            string mdpPathPcm = Path.Combine(stPath, "mucompcm.bin");
            string decidePcm = "";

            if (!File.Exists(mucPathPcm))
            {
                if (!File.Exists(mdpPathPcm))
                {
                    return;
                }
                decidePcm = mdpPathPcm;
            }
            else
            {
                decidePcm = mucPathPcm;
            }

            byte[] pcmdat = File.ReadAllBytes(decidePcm);
            mucomADPCM2PCM.initial(pcmdat);
            List<mucomADPCM2PCM.mucomPCMInfo> lstInfo = mucomADPCM2PCM.lstMucomPCMInfo;

            foreach (mucomADPCM2PCM.mucomPCMInfo info in lstInfo)
            {
                clsPcmDatSeq pds = new clsPcmDatSeq(
                    enmPcmDefineType.Mucom88
                    , info.no
                    , info.name
                    , 8000
                    , 150
                    , enmChipType.YM2612
                    , false
                    , -1
                    );

                instPCMDatSeq.Add(pds);
            }

        }

        #region analyze

        /// <summary>
        /// ソースを分類する
        /// </summary>
        public int Analyze(List<Line> src)
        {
            log.Write("テキスト解析開始");
            lineNumber = 0;

            bool voiceSetmode = false;
            List<string> voiceTemp = new List<string>();
            int voiceRow = 6;
            string s2 = "";

            foreach (Line line in src)
            {

                string s = line.Txt;
                int lineNumber = line.Num;
                string fn = line.Fn;

                if (string.IsNullOrEmpty(s)) continue;

                //voicedata作成
                if (voiceSetmode)
                {
                    if(s.IndexOf("  ") != 0)
                    {
                        continue;
                    }

                    voiceTemp.Add(s.Substring(0, s.IndexOf(";") < 0 ? s.Length : s.IndexOf(";")));
                    if (voiceTemp.Count == voiceRow)
                    {
                        SetInstrument(voiceTemp, fn, lineNumber);
                        voiceTemp.Clear();
                        voiceSetmode = false;
                    }
                    continue;
                }

                if (s[0] == '#' && s.Length > 1)
                {
                    //マクロか
                    if ((s[1] == ' ' || s[1] == '\t' || s[1] == '*') && s.Length > 2)
                    {
                        // Alies
                        AddAlies(s.Substring(s.IndexOf('*')), fn, lineNumber);
                        continue;
                    }
                    else
                    {
                        info.AddInformation(s.Substring(1), lineNumber, fn, chips);
                        continue;
                    }
                }

                for(int i = 0; i < PART_OPN2.Length; i++)
                {
                    if (s.Length < 1) continue;
                    if (s.Length > 2 && s[0] == PART_OPN2[i] && (s[1] == ' ' || s[1] == '\t'))
                    {
                        // Part
                        AddPart(s,0,i, fn, lineNumber);
                        continue;
                    }
                }
                for (int i = 0; i < PART_DCSG.Length; i++)
                {
                    if (s.Length < 1) continue;
                    if (s.Length > 2 && s[0] == PART_DCSG[i] && (s[1] == ' ' || s[1] == '\t'))
                    {
                        // Part
                        AddPart(s, 1, i, fn, lineNumber);
                        continue;
                    }
                }

                if (s.IndexOf("  @") == 0)
                {
                    voiceSetmode = true;
                    voiceTemp.Clear();
                    voiceTemp.Add(s);
                    voiceRow = s.IndexOf("  @%") == 0 ? 8 : 6;
                    continue;
                }

                if (s.IndexOf("'@") == 0)
                {
                    AddInstrument(s.Substring(2), fn, lineNumber);
                    continue;
                }

                //{
                //    multiLine = true;
                //    s2 = s2.Substring(1);

                //    if (s2.IndexOf("}") > -1)
                //    {
                //        multiLine = false;
                //        s2 = s2.Substring(0, s2.IndexOf("}")).Trim();
                //        // Information
                //        info.AddInformation(s2, lineNumber, fn, chips);
                //    }
                //    continue;
                //}
                //else if (s2.IndexOf("@") == 0)
                //{
                //    // Instrument
                //    AddInstrument(s2, fn, lineNumber);
                //    continue;
                //}
                //else if (s2.IndexOf("%") == 0)
                //{
                //    // Alies
                //    AddAlies(s2, fn, lineNumber);
                //    continue;
                //}
                //else
                //{
                //    // Part
                //    AddPart(s2, fn, lineNumber);
                //    continue;
                //}

            }

            // 定義中のToneDoublerがあればここで定義完了
            if (toneDoublerCounter != -1)
            {
                toneDoublerCounter = -1;
                SetInstToneDoubler();
            }

            // チェック1定義されていない名称を使用したパートが存在するか

            foreach (string p in partData.Keys)
            {
                bool flg = false;
                foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
                {
                    foreach (ClsChip chip in kvp.Value)
                    {
                        if (chip.ChannelNameContains(p))
                        {
                            flg = true;
                            break;
                        }
                    }
                }
                if (!flg)
                {
                    msgBox.setWrnMsg(string.Format(
                        msg.get("E01000")
                        , p.Substring(0, 2).Trim() + int.Parse(p.Substring(2, 2)).ToString()));
                    flg = false;
                }
            }

            //if (info.userClockCount != 0) info.clockCount = info.userClockCount;

            log.Write("テキスト解析完了");
            return 0;

        }

        private int AddInstrument(string buf, string srcFn, int lineNumber)
        {
            if (buf == null || buf.Length < 2)
            {
                msgBox.setWrnMsg(msg.get("E01001"), srcFn, lineNumber);
                return -1;
            }

            string s = buf.Substring(1).TrimStart();

            // FMの音色を定義中の場合
            //if (instrumentCounter != -1)
            //{

            //    return SetInstrument(s, srcFn, lineNumber);

            //}

            // WaveFormの音色を定義中の場合
            if (wfInstrumentCounter != -1)
            {

                return SetWfInstrument(s, srcFn, lineNumber);

            }

            char t = s.ToUpper()[0];
            if (toneDoublerCounter != -1)
            {
                if (t == 'F' || t == 'N' || t == 'M' || t == 'L' || t == 'P' || t == 'E' || t == 'T' || t == 'H')
                {
                    toneDoublerCounter = -1;
                    SetInstToneDoubler();
                }
            }

            switch (t)
            {
                //case 'F':
                //    instrumentBufCache = new byte[Const.INSTRUMENT_SIZE - 8];
                //    instrumentCounter = 0;
                //    SetInstrument(s.Substring(1).TrimStart(), srcFn, lineNumber);
                //    return 0;

                //case 'N':
                //    instrumentBufCache = new byte[Const.INSTRUMENT_SIZE];
                //    instrumentCounter = 0;
                //    SetInstrument(s.Substring(1).TrimStart(), srcFn, lineNumber);
                //    return 0;

                //case 'M':
                //    instrumentBufCache = new byte[Const.INSTRUMENT_SIZE];
                //    instrumentCounter = 0;
                //    SetInstrument(s.Substring(1).TrimStart(), srcFn, lineNumber);
                //    return 0;

                //case 'L':
                //    instrumentBufCache = new byte[Const.OPL_INSTRUMENT_SIZE];
                //    instrumentCounter = 0;
                //    SetInstrument(s.Substring(1).TrimStart(), srcFn, lineNumber);
                //    return 0;

                case 'P':
                    definePCMInstrument(srcFn, s, lineNumber);
                    return 0;

                case 'E':
                    try
                    {
                        instrumentCounter = -1;
                        string[] vs = s.Substring(1).Trim().Split(new string[] { "," }, StringSplitOptions.None);
                        int[] env = null;
                        env = new int[9];
                        int num = int.Parse(vs[0]);
                        for (int i = 0; i < env.Length; i++)
                        {
                            if (i == 8)
                            {
                                if (vs.Length == 8) env[i] = (int)enmChipType.SN76489;
                                else env[i] = (int)GetChipType(vs[8]);
                                continue;
                            }
                            env[i] = int.Parse(vs[i]);
                        }

                        enmChipType chiptype = GetChipType(env[8]);
                        if (chips.ContainsKey(chiptype) && chips[chiptype][0].Envelope != null)
                        {
                            CheckEnvelopeVolumeRange(srcFn, lineNumber, env, chips[chiptype][0].Envelope.Max, chips[chiptype][0].Envelope.Min);
                            if (env[7] == 0) env[7] = 1;
                        }
                        else
                        {
                            msgBox.setWrnMsg(msg.get("E01004"), srcFn, lineNumber);
                        }

                        if (instENV.ContainsKey(num))
                        {
                            instENV.Remove(num);
                        }
                        instENV.Add(num, env);
                    }
                    catch
                    {
                        msgBox.setWrnMsg(msg.get("E01005"), srcFn, lineNumber);
                    }
                    return 0;

                case 'T':
                    try
                    {
                        instrumentCounter = -1;

                        if (s.ToUpper()[1] != 'D') return 0;

                        toneDoublerBufCache.Clear();
                        StoreToneDoublerBuffer(s.ToUpper().Substring(2).TrimStart(), srcFn, lineNumber);
                    }
                    catch
                    {
                        msgBox.setWrnMsg(msg.get("E01006"), srcFn, lineNumber);
                    }
                    return 0;

                case 'H':
                    wfInstrumentBufCache = new byte[Const.WF_INSTRUMENT_SIZE];
                    wfInstrumentCounter = 0;
                    SetWfInstrument(s.Substring(1).TrimStart(), srcFn, lineNumber);
                    return 0;

            }

            // ToneDoublerを定義中の場合
            if (toneDoublerCounter != -1)
            {
                return StoreToneDoublerBuffer(s.ToUpper(), srcFn, lineNumber);
            }

            return 0;
        }

        private void definePCMInstrument(string srcFn, string s, int lineNumber)
        {
            try
            {
                string[] vs = s.Substring(1).Trim().Split(new string[] { "," }, StringSplitOptions.None);
                if (vs.Length < 1) throw new ArgumentOutOfRangeException();
                for (int i = 0; i < vs.Length; i++) vs[i] = vs[i].Trim();

                switch (vs[0][0])
                {
                    case 'D':
                        definePCMInstrumentRawData(srcFn, lineNumber, vs);
                        break;
                    case 'I':
                        definePCMInstrumentSet(srcFn, lineNumber, vs);
                        break;
                    default:
                        definePCMInstrumentEasy(srcFn, lineNumber, vs);
                        break;
                }

                return;
            }
            catch
            {
                msgBox.setWrnMsg(msg.get("E01003"), srcFn, lineNumber);
            }
        }

        /// <summary>
        /// '@ P No , "FileName" , [BaseFreq] , Volume ( , [ChipName] , [Option] )
        /// </summary>
        private void definePCMInstrumentEasy(string srcFn, int lineNumber, string[] vs)
        {
            instrumentCounter = -1;
            enmChipType enmChip = enmChipType.YM2612;

            int num = Common.ParseNumber(vs[0]);

            string fn = vs[1].Trim().Trim('"');

            int fq;
            try
            {
                fq = Common.ParseNumber(vs[2]);
            }
            catch
            {
                fq = -1;
            }

            int vol;
            try
            {
                vol = Common.ParseNumber(vs[3]);
            }
            catch
            {
                vol = 100;
            }

            bool isSecondary = false;

            if (vs.Length > 4)
            {
                enmChip = GetChipTypeForPCM(srcFn, lineNumber, vs[4], out isSecondary);
                if (enmChip == enmChipType.None) return;
            }

            if (info.format == enmFormat.XGM)
            {
                if (enmChip != enmChipType.YM2612X)
                {
                    msgBox.setErrMsg(msg.get("E01017"));
                    return;
                }
            }

            int lp = -1;

            if (vs.Length > 5)
            {
                try
                {
                    lp = Common.ParseNumber(vs[5]);
                }
                catch
                {
                    lp = -1;
                }
            }

            if (lp == -1 && enmChip == enmChipType.YM2610B)
            {
                lp = 0;
            }

            instPCMDatSeq.Add(new clsPcmDatSeq(
                enmPcmDefineType.Easy
                , num
                , fn
                , fq
                , vol
                , enmChip
                , isSecondary
                , lp
                ));

            //if (instPCM.ContainsKey(num))
            //{
            //    instPCM.Remove(num);
            //}
            //instPCM.Add(num, new clsPcm(num, pcmDataSeqNum++, enmChip, isSecondary, fn, fq, vol, 0, 0, 0, lp, false, 8000));
        }

        /// <summary>
        /// '@ PD "FileName" , ChipName , [SrcStartAdr] , [DesStartAdr] , [Length] , [Option]
        /// </summary>
        private void definePCMInstrumentRawData(string srcFn, int lineNumber, string[] vs)
        {

            string FileName = vs[0].Substring(1).Trim().Trim('"');
            enmChipType ChipName = GetChipTypeForPCM(srcFn, lineNumber, vs[1], out bool isSecondary);

            if (info.format == enmFormat.XGM)
            {
                if (ChipName != enmChipType.YM2612X)
                {
                    msgBox.setErrMsg(msg.get("E01017"));
                    return;
                }
            }

            int SrcStartAdr = 0;
            if (vs.Length > 2 && !string.IsNullOrEmpty(vs[2].Trim()))
            {
                SrcStartAdr = Common.ParseNumber(vs[2]);
            }
            int DesStartAdr = 0;
            if (vs.Length > 3 && !string.IsNullOrEmpty(vs[3].Trim()))
            {
                DesStartAdr = Common.ParseNumber(vs[3]);
            }
            int Length = -1;
            if (vs.Length > 4 && !string.IsNullOrEmpty(vs[4].Trim()))
            {
                Length = Common.ParseNumber(vs[4]);
            }
            string[] Option = null;
            if (vs.Length > 5)
            {
                Option = new string[vs.Length - 5];
                Array.Copy(vs, 5, Option, 0, vs.Length - 5);
            }

            instPCMDatSeq.Add(new clsPcmDatSeq(
                enmPcmDefineType.RawData
                , FileName
                , ChipName
                , isSecondary
                , SrcStartAdr
                , DesStartAdr
                , Length
                , Option
                ));

        }

        /// <summary>
        /// '@ PI No , ChipName , [BaseFreq] , StartAdr , EndAdr , [LoopAdr] , [Option]
        /// </summary>
        private void definePCMInstrumentSet(string srcFn, int lineNumber, string[] vs)
        {
            int num = Common.ParseNumber(vs[0].Substring(1));
            enmChipType ChipName = GetChipTypeForPCM(srcFn, lineNumber, vs[1], out bool isSecondary);
            if (ChipName == enmChipType.None) return;

            if (info.format == enmFormat.XGM)
            {
                if (ChipName != enmChipType.YM2612X)
                {
                    msgBox.setErrMsg(msg.get("E01017"));
                    return;
                }
            }

            if (!chips[ChipName][0].CanUsePICommand())
            {
                msgBox.setWrnMsg(string.Format(msg.get("E10018"), chips[ChipName][0].Name));
                return;
            }

            int BaseFreq;
            //if (vs.Length > 2 && !string.IsNullOrEmpty(vs[2].Trim()))
            //{
            try
            {
                BaseFreq = Common.ParseNumber(vs[2]);
            }
            catch
            {
                BaseFreq = 8000;
            }

            //StartAdr省略不可
            int StartAdr = 0;
            StartAdr = Common.ParseNumber(vs[3]);

            //EndAdr省略不可(RF5C164は設定不可)
            int EndAdr = 0;
            if (ChipName != enmChipType.RF5C164)
            {
                EndAdr = Common.ParseNumber(vs[4]);
            }
            else
            {
                if (!string.IsNullOrEmpty(vs[4].ToString()))
                    throw new ArgumentOutOfRangeException();
            }

            //LoopAdr(RF5C164は省略不可)
            int LoopAdr;
            if (ChipName != enmChipType.RF5C164)
            {
                LoopAdr = (ChipName != enmChipType.YM2610B) ? -1 : 0;
                if (vs.Length > 5 && !string.IsNullOrEmpty(vs[5].Trim()))
                {
                    LoopAdr = Common.ParseNumber(vs[5]);
                }
            }
            else
            {
                LoopAdr = Common.ParseNumber(vs[5]);
            }

            string[] Option = null;
            if (vs.Length > 6)
            {
                Option = new string[vs.Length - 6];
                Array.Copy(vs, 6, Option, 0, vs.Length - 6);
            }
            if (ChipName == enmChipType.YM2610B)
            {
                if (Option == null || Option.Length < 1)
                {
                    LoopAdr = 0;
                }
                else
                {
                    LoopAdr = 1;
                    if (Option[0].Trim() != "1")
                    {
                        LoopAdr = 0;
                    }
                }
            }

            instPCMDatSeq.Add(new clsPcmDatSeq(
                enmPcmDefineType.Set
                , num
                , ChipName
                , isSecondary
                , BaseFreq
                , StartAdr
                , EndAdr
                , LoopAdr
                , Option
                ));

        }

        private static void CheckEnvelopeVolumeRange(string srcFn, int lineNumber, int[] env, int max, int min)
        {
            for (int i = 0; i < env.Length - 1; i++)
            {
                if (i != 1 && i != 4 && i != 7) continue;

                if (env[i] > max)
                {
                    env[i] = max;
                    msgBox.setWrnMsg(string.Format(msg.get("E01007"), max), srcFn, lineNumber);
                }
                if (env[i] < min)
                {
                    env[i] = min;
                    msgBox.setWrnMsg(string.Format(msg.get("E01008"), min), srcFn, lineNumber);
                }
            }
        }


        private enmChipType GetChipTypeForPCM(string srcFn, int lineNumber, string strChip, out bool isSecondary)
        {
            enmChipType enmChip = enmChipType.YM2612;
            string chipName = strChip.Trim().ToUpper();
            isSecondary = false;
            if (chipName == "") return enmChipType.YM2612;

            if (chipName.IndexOf(Information.PRIMARY) >= 0)
            {
                isSecondary = false;
                chipName = chipName.Replace(Information.PRIMARY, "");
            }
            else if (chipName.IndexOf(Information.SECONDARY) >= 0)
            {
                isSecondary = true;
                chipName = chipName.Replace(Information.SECONDARY, "");
            }

            if (!GetChip(chipName).CanUsePcm)
            {
                msgBox.setWrnMsg(string.Format(msg.get("E01002"), chipName), srcFn, lineNumber);
                return enmChipType.None;
            }
            enmChip = GetChipType(chipName);

            return enmChip;
        }

        private enmChipType GetChipType(string chipN)
        {
            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                foreach (ClsChip chip in kvp.Value)
                {
                    if (chip.Name.ToUpper().Trim() == chipN.ToUpper().Trim()
                        || chip.ShortName.ToUpper().Trim() == chipN.ToUpper().Trim())
                    {
                        return kvp.Key;
                    }
                }
            }

            return enmChipType.None;
        }

        private enmChipType GetChipType(int chipNum)
        {
            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                if ((int)kvp.Key == chipNum)
                {
                    return kvp.Key;
                }
            }

            return enmChipType.None;
        }


        private ClsChip GetChip(string chipN)
        {
            string n = chipN.ToUpper().Trim();

            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                foreach (ClsChip chip in kvp.Value)
                {
                    if (n == chip.Name.ToUpper()) return chip;
                    if (n == chip.ShortName.ToUpper()) return chip;
                }
            }

            return null;
        }

        private int AddAlies(string buf, string srcFn, int lineNumber)
        {
            int? name = null;
            string data = "";

            int i = buf.Trim().IndexOf('{');
            if (i < 0)
            {
                //空白による区切りが見つからない場合は無視する
                return 0;
            }

            int len = 0;
            name = Common.GetNumsFromString(buf.Trim(), 1, ref len);
            data = buf.Trim().Substring(i).Trim();
            if (data.LastIndexOf('}') < data.LastIndexOf(';'))
            {
                data = data.Substring(0, data.LastIndexOf(';')).Trim();
            }
            if (data.Length < 2 || data[0] != '{' || data[data.Length - 1] != '}')
            {
                msgBox.setWrnMsg(msg.get("E01018"), srcFn, lineNumber);
                return -1;
            }
            data = data.Substring(1, data.Length - 2);
            if (name == null)
            {
                //エイリアス指定がない場合は警告とする
                msgBox.setWrnMsg(msg.get("E01009"), srcFn, lineNumber);
                return -1;
            }
            if (data == "")
            {
                //データがない場合は警告する
                msgBox.setWrnMsg(msg.get("E01010"), srcFn, lineNumber);
            }

            if (aliesData.ContainsKey((int)name))
            {
                aliesData.Remove((int)name);
            }
            aliesData.Add((int)name, new Line("", lineNumber, data));

            return 0;
        }

        private int AddPart(string buf,int tp,int ch, string srcFn, int lineNumber)
        {
            List<string> part = new List<string>();
            string data = "";

            int i = buf.IndexOfAny(new char[] { ' ', '\t' });
            if (i < 0)
            {
                //空白による区切りが見つからない場合は無視する
                return 0;
            }

            part.Add((tp == 0 ? PART_OPN2[ch] : PART_DCSG[ch]).ToString());
            data = buf.Substring(i).Trim();

            //コメントのカット
            if (data.IndexOf(";") >= 0 && data.Length>0)
            {
                int cnt = 0;
                bool f = false;
                while (cnt < data.Length)
                {
                    if (data[cnt] == '"')
                    {
                        cnt++;
                        while (data[cnt++] != '"' && cnt<data.Length) ;
                    }
                    if (data[cnt] == ';')
                    {
                        f = true;
                        break;
                    }
                    cnt++;
                }
                if (f) data = data.Substring(0,cnt);
            }

            if (part == null)
            {
                //パート指定がない場合は警告とする
                msgBox.setWrnMsg(msg.get("E01011"), srcFn, lineNumber);
                return -1;
            }
            if (data == "")
            {
                //データがない場合は無視する
                return 0;
            }

            foreach (string p in part)
            {
                if (!partData.ContainsKey(p))
                {
                    partData.Add(p, new List<Line>());
                }
                partData[p].Add(new Line(srcFn, lineNumber, data));
            }

            return 0;
        }

        
        private int SetInstrument(List<string> vals, string srcFn, int lineNumber)
        {

            try
            {
                bool nrmMode = true;
                if (vals[0].IndexOf("@%") >= 0)
                {
                    nrmMode = false;
                }
                int[] inst = null;
                mucomVoice voi = new mucomVoice();
                if (nrmMode)
                {
                    voi.type = 0;
                    voi.Name = "";
                    inst = new int[2 + 4 * 9];
                    //inst[0]に音色番号をセット
                    bool ok = GetNums( inst, 0, 1, vals[0], vals[0].IndexOf("@") + 1);
                    voi.No = inst[0];
                    if (!ok) throw new ArgumentException();
                    //inst[1],[2]に音色番号をセット
                    ok = GetNums( inst, 0, 2, vals[1], 2);
                    if (!ok) throw new ArgumentException();
                    //inst[3]から[12]に音色番号をセット
                    ok = GetNums( inst, 2, 9, vals[2], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums( inst, 11, 9, vals[3], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums( inst, 20, 9, vals[4], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums( inst, 29, 9, vals[5], 2);
                    if (!ok) throw new ArgumentException();
                    voi.data = new byte[38];
                    for (int i = 0; i < voi.data.Length; i++) voi.data[i] = (byte)inst[i];

                }
                else
                {
                    voi.type = 1;
                    voi.Name = "";
                    inst = new int[25];
                    bool ok = GetNums(inst, 0, 1, vals[0], vals[0].IndexOf("%") + 1);
                    voi.No = inst[0];
                    if (!ok) throw new ArgumentException();
                    ok = GetNums(inst, 0, 4, vals[1], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums(inst, 4, 4, vals[2], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums(inst, 8, 4, vals[3], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums(inst, 12, 4, vals[4], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums(inst, 16, 4, vals[5], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums(inst, 20, 4, vals[6], 2);
                    if (!ok) throw new ArgumentException();
                    ok = GetNums(inst, 24, 1, vals[7], 2);
                    if (!ok) throw new ArgumentException();
                    voi.data = new byte[25];
                    for (int i = 0; i < voi.data.Length; i++) voi.data[i] = (byte)inst[i];
                }

                //すでに定義済みの場合はいったん削除する(後に定義されたものが優先)
                if (instFM.ContainsKey(voi.No))
                {
                    instFM.Remove(voi.No);
                }

                instFM.Add(voi.No, voi);

                instrumentCounter = -1;
            }
            catch
            {
                msgBox.setErrMsg(msg.get("E01012"), srcFn, lineNumber);
            }

            return 0;
        }

        //private int SetInstrument(string vals, string srcFn, int lineNumber)
        //{

        //    try
        //    {
        //        instrumentCounter = GetNums(instrumentBufCache, instrumentCounter, vals);

        //        if (instrumentCounter == instrumentBufCache.Length)
        //        {
        //            //すでに定義済みの場合はいったん削除する(後に定義されたものが優先)
        //            if (instFM.ContainsKey(instrumentBufCache[0]))
        //            {
        //                instFM.Remove(instrumentBufCache[0]);
        //            }


        //            if (instrumentBufCache.Length == Const.INSTRUMENT_SIZE)
        //            {
        //                //M
        //                instFM.Add(instrumentBufCache[0], instrumentBufCache);
        //            }
        //            else if (instrumentBufCache.Length == Const.OPL_INSTRUMENT_SIZE)
        //            {
        //                //OPL
        //                instFM.Add(instrumentBufCache[0], instrumentBufCache);
        //            }
        //            else
        //            {
        //                //F
        //                instFM.Add(instrumentBufCache[0], ConvertFtoM(instrumentBufCache));
        //            }

        //            instrumentCounter = -1;
        //        }
        //    }
        //    catch
        //    {
        //        msgBox.setErrMsg(msg.get("E01012"), srcFn, lineNumber);
        //    }

        //    return 0;
        //}

        private int SetWfInstrument(string vals, string srcFn, int lineNumber)
        {

            try
            {
                wfInstrumentCounter = GetNums(wfInstrumentBufCache, wfInstrumentCounter, vals);

                if (wfInstrumentCounter == wfInstrumentBufCache.Length)
                {
                    if (instWF.ContainsKey(wfInstrumentBufCache[0]))
                    {
                        instWF.Remove(wfInstrumentBufCache[0]);
                    }
                    instWF.Add(wfInstrumentBufCache[0], wfInstrumentBufCache);

                    wfInstrumentCounter = -1;
                }
            }
            catch
            {
                msgBox.setErrMsg(msg.get("E01013"), srcFn, lineNumber);
            }

            return 0;
        }

        private bool GetNums(int[] Buf, int strIndex,int strLength, string vals,int startIndex)
        {
            try
            {
                string n = "";
                string h = "";
                int hc = -1;
                int cnt = 0;

                for (int i = startIndex; i < vals.Length; i++)
                {
                    if (cnt == strLength)
                    {
                        return true;
                    }

                    char c = vals[i];
                    if (c == ' ')
                    {
                        continue;
                    }
                    if (c == '$')
                    {
                        h = "";
                        hc = 0;
                        continue;
                    }

                    if (hc > -1)
                    {
                        if (((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                        {
                            h += c;
                            continue;
                        }
                        else
                        {
                            int j = int.Parse(h, System.Globalization.NumberStyles.HexNumber);
                            Buf[strIndex] = j;
                            strIndex++;
                            h = "";
                            hc = -1;
                            cnt++;
                            continue;
                        }
                    }
                    else
                    {
                        if ((c >= '0' && c <= '9') || c == '-')
                        {
                            n = n + c;
                            continue;
                        }
                        else
                        {
                            int j;
                            if(!int.TryParse(n,out j))
                            {
                                continue;
                            }
                            Buf[strIndex] = j;
                            strIndex++;
                            n = "";
                            hc = -1;
                            cnt++;
                            continue;
                        }
                    }
                }

                if (hc > -1)
                {
                    if (h != "")
                    {
                        int j = int.Parse(h, System.Globalization.NumberStyles.HexNumber);
                        Buf[strIndex] = j;
                        strIndex++;
                        cnt++;
                    }
                }
                else
                {
                    if (n != "")
                    {
                        int j = int.Parse(n);
                        Buf[strIndex] = j;
                        strIndex++;
                        cnt++;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private int GetNums(byte[] aryBuf, int aryIndex, string vals)
        {
            string n = "";
            string h = "";
            int hc = -1;
            int i = 0;

            foreach (char c in vals)
            {
                if (c == '$')
                {
                    hc = 0;
                    continue;
                }

                if (hc > -1 && ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                {
                    h += c;
                    hc++;
                    if (hc == 2)
                    {
                        i = int.Parse(h, System.Globalization.NumberStyles.HexNumber);
                        aryBuf[aryIndex] = (byte)(i & 0xff);
                        aryIndex++;
                        h = "";
                        hc = -1;
                    }
                    continue;
                }

                if ((c >= '0' && c <= '9') || c == '-')
                {
                    n = n + c.ToString();
                    continue;
                }

                if (int.TryParse(n, out i))
                {
                    aryBuf[aryIndex] = (byte)(i & 0xff);
                    aryIndex++;
                    n = "";
                }
            }

            if (!string.IsNullOrEmpty(n))
            {
                if (int.TryParse(n, out i))
                {
                    aryBuf[aryIndex] = (byte)(i & 0xff);
                    aryIndex++;
                    n = "";
                }
            }

            return aryIndex;
        }

        private int StoreToneDoublerBuffer(string vals, string srcFn, int lineNumber)
        {
            string n = "";
            string h = "";
            int hc = -1;
            int i;

            try
            {
                foreach (char c in vals)
                {
                    if (c == '$')
                    {
                        hc = 0;
                        continue;
                    }

                    if (hc > -1 && ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                    {
                        h += c;
                        hc++;
                        if (hc == 2)
                        {
                            i = int.Parse(h, System.Globalization.NumberStyles.HexNumber);
                            toneDoublerBufCache.Add(i);
                            toneDoublerCounter++;
                            h = "";
                            hc = -1;
                        }
                        continue;
                    }

                    if ((c >= '0' && c <= '9') || c == '-')
                    {
                        n = n + c.ToString();
                        continue;
                    }

                    if (int.TryParse(n, out i))
                    {
                        toneDoublerBufCache.Add(i);
                        toneDoublerCounter++;
                        n = "";
                    }
                }

                if (!string.IsNullOrEmpty(n))
                {
                    if (int.TryParse(n, out i))
                    {
                        toneDoublerBufCache.Add(i);
                        toneDoublerCounter++;
                        n = "";
                    }
                }

            }
            catch
            {
                msgBox.setErrMsg(msg.get("E01014"), srcFn, lineNumber);
            }

            return 0;
        }

        private void SetInstToneDoubler()
        {
            if (toneDoublerBufCache.Count < 10)
            {
                toneDoublerBufCache.Clear();
                toneDoublerCounter = -1;
                return;
            }

            int num = toneDoublerBufCache[0];
            int counter = 1;
            List<clsTD> lstTD = new List<clsTD>();
            while (counter < toneDoublerBufCache.Count)
            {
                clsTD td = new clsTD(
                    toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    , toneDoublerBufCache[counter++]
                    );
                lstTD.Add(td);
            }

            clsToneDoubler toneDoubler = new clsToneDoubler(num, lstTD);
            if (instToneDoubler.ContainsKey(num))
            {
                instToneDoubler.Remove(num);
            }
            instToneDoubler.Add(num, toneDoubler);
            toneDoublerBufCache.Clear();
            toneDoublerCounter = -1;
        }

        private byte[] ConvertFtoM(byte[] instrumentBufCache)
        {
            byte[] ret = new byte[Const.INSTRUMENT_SIZE];

            ret[0] = instrumentBufCache[0];

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < Const.INSTRUMENT_OPERATOR_SIZE; i++)
                {
                    ret[j * Const.INSTRUMENT_M_OPERATOR_SIZE + i + 1] = instrumentBufCache[j * Const.INSTRUMENT_OPERATOR_SIZE + i + 1];
                }
            }

            ret[Const.INSTRUMENT_SIZE - 2] = instrumentBufCache[Const.INSTRUMENT_SIZE - 10];
            ret[Const.INSTRUMENT_SIZE - 1] = instrumentBufCache[Const.INSTRUMENT_SIZE - 9];

            return ret;
        }

        private byte[] ConvertMucom(int[] instrumentBufCache)
        {
            byte[] ret = new byte[Const.INSTRUMENT_SIZE];

            ret[0] = (byte)instrumentBufCache[0];

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < Const.INSTRUMENT_OPERATOR_SIZE; i++)
                {
                    ret[j * Const.INSTRUMENT_M_OPERATOR_SIZE + i + 1] = (byte)instrumentBufCache[j * Const.INSTRUMENT_OPERATOR_SIZE + i + 3];
                }
            }

            ret[Const.INSTRUMENT_SIZE - 2] = (byte)instrumentBufCache[1];
            ret[Const.INSTRUMENT_SIZE - 1] = (byte)instrumentBufCache[0];

            return ret;
        }

        private byte[] ConvertMucomEX(int[] instrumentBufCache)
        {
            byte[] ret = new byte[Const.INSTRUMENT_SIZE];

            ret[0] = (byte)instrumentBufCache[0];

            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < Const.INSTRUMENT_OPERATOR_SIZE; i++)
                {
                    ret[j * Const.INSTRUMENT_M_OPERATOR_SIZE + i + 1] = (byte)instrumentBufCache[j * Const.INSTRUMENT_OPERATOR_SIZE + i + 3];
                }
            }

            ret[Const.INSTRUMENT_SIZE - 2] = (byte)instrumentBufCache[1];
            ret[Const.INSTRUMENT_SIZE - 1] = (byte)instrumentBufCache[0];

            return ret;
        }

        #endregion



        public List<byte> dat = null;
        //xgm music data
        public List<byte> xdat = null;
        //xgm keyOnDataList
        public List<byte> xgmKeyOnData = null;

        public double dSample = 0.0;
        public long lClock = 0L;
        private double sampleB = 0.0;
        public string lyric = "";

        public long loopOffset = -1L;
        public long loopClock = -1L;
        public long loopSamples = -1L;

        private Random rnd = new Random();



        public byte[] Vgm_getByteData(Dictionary<string, List<MML>> mmlData)
        {

            dat = new List<byte>();

            log.Write("ヘッダー情報作成");
            MakeHeader();

            int endChannel = 0;
            newStreamID = -1;
            int totalChannel = 0;
            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                foreach (ClsChip chip in kvp.Value)
                {
                    totalChannel += chip.ChMax;
                }
            }

            log.Write("MML解析開始");
            long waitCounter = 0;
            do
            {
                foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
                {
                    foreach (ClsChip chip in kvp.Value)
                    {
                        log.Write(string.Format("Chip [{0}]", chip.Name));

                        partWork pw;
                        for (int i = 0; i < chip.lstPartWork.Count; i++)
                        {
                            pw = chip.lstPartWork[
                                chip.ReversePartWork
                                ? (chip.lstPartWork.Count - 1 - i)
                                : i
                                ];
                            partWorkByteData(pw);
                        }
                        if (chip.SupportReversePartWork) chip.ReversePartWork = !chip.ReversePartWork;

                        log.Write("channelを跨ぐコマンド向け処理");
                        //未使用のパートの場合は処理を行わない
                        if (!chip.use) continue;
                        chip.MultiChannelCommand();
                    }
                }

                log.Write("全パートのうち次のコマンドまで一番近い値を求める");
                waitCounter = long.MaxValue;
                foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
                {
                    foreach (ClsChip chip in kvp.Value)
                    {
                        for (int ch = 0; ch < chip.lstPartWork.Count; ch++)
                        {

                            partWork cpw = chip.lstPartWork[ch];

                            if (!cpw.chip.use) continue;

                            //note
                            if (cpw.waitKeyOnCounter > 0)
                            {
                                waitCounter = Math.Min(waitCounter, cpw.waitKeyOnCounter);
                            }
                            else if (cpw.waitCounter > 0)
                            {
                                waitCounter = Math.Min(waitCounter, cpw.waitCounter);
                            }

                            //bend
                            if (cpw.bendWaitCounter != -1)
                            {
                                waitCounter = Math.Min(waitCounter, cpw.bendWaitCounter);
                            }

                            //lfoとenvelopeは音長によるウエイトカウントが存在する場合のみ対象にする。(さもないと、曲のループ直前の効果を出せない)
                            if (waitCounter > 0)
                            {
                                //lfo
                                for (int lfo = 0; lfo < 1; lfo++)
                                {
                                    if (!cpw.lfo[lfo].sw) continue;
                                    if (cpw.lfo[lfo].waitCounter <= 0) continue;

                                    waitCounter = Math.Min(waitCounter, cpw.lfo[lfo].waitCounter);
                                }

                                //envelope
                                if (cpw.envelopeMode && cpw.envIndex != -1)
                                {
                                    waitCounter = Math.Min(waitCounter, GetWaitCounter(1));
                                }
                            }

                            //pcm
                            if (cpw.pcmWaitKeyOnCounter > 0)
                            {
                                waitCounter = Math.Min(waitCounter, cpw.pcmWaitKeyOnCounter);
                            }

                        }

                    }
                }

                log.Write("全パートのwaitcounterを減らす");
                if (waitCounter != long.MaxValue)
                {

                    // waitcounterを減らす

                    foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
                    {
                        foreach (ClsChip chip in kvp.Value)
                        {
                            foreach (partWork pw in chip.lstPartWork)
                            {

                                if (pw.waitKeyOnCounter > 0) pw.waitKeyOnCounter -= waitCounter;

                                if (pw.waitCounter > 0) pw.waitCounter -= waitCounter;

                                if (pw.bendWaitCounter > 0) pw.bendWaitCounter -= waitCounter;

                                for (int lfo = 0; lfo < 1; lfo++)
                                {
                                    if (!pw.lfo[lfo].sw) continue;
                                    if (pw.lfo[lfo].waitCounter == -1) continue;

                                    if (pw.lfo[lfo].waitCounter > 0)
                                    {
                                        pw.lfo[lfo].waitCounter -= waitCounter;
                                        if (pw.lfo[lfo].waitCounter < 0) pw.lfo[lfo].waitCounter = 0;
                                    }
                                }

                                if (pw.pcmWaitKeyOnCounter > 0)
                                {
                                    pw.pcmWaitKeyOnCounter -= waitCounter;
                                }

                                //if (pw.envelopeMode && pw.envIndex != -1)
                                //{
                                //    pw.envCounter -= (int)waitCounter;
                                //}
                            }
                        }
                    }

                    // wait発行

                    lClock += waitCounter;
                    dSample += (long)(waitCounter * (256 - info.timerB) * 1152.0 / (7987200.0 / 2.0) * 44100.0);
                    if (useJumpCommand == 0)
                    {
                        long w = waitCounter;

                        //1152.0          : TimerBDelta(From OPNA application manual)
                        //7987200.0 / 2.0 : MasterClock / Div
                        //44100.0         : VGMfile freq
                        w = (long)(w * (256 - info.timerB) * 1152.0 / (7987200.0 / 2.0) * 44100.0);

                        if (ym2612[0].lstPartWork[5].pcmWaitKeyOnCounter <= 0)//== -1)
                        {
                            OutWaitNSamples((long)(w));
                        }
                        else
                        {
                            OutWaitNSamplesWithPCMSending(ym2612[0].lstPartWork[5], w);
                        }
                    }
                }

                log.Write("終了パートのカウント");
                endChannel = 0;
                foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
                {
                    foreach (ClsChip chip in kvp.Value)
                    {
                        foreach (partWork pw in chip.lstPartWork)
                        {
                            if (!pw.chip.use) endChannel++;
                            else if (pw.dataEnd && pw.waitCounter < 1) endChannel++;
                            else if (loopOffset != -1 && pw.dataEnd && pw.envIndex == 3) endChannel++;
                        }
                    }
                }

            } while (endChannel < totalChannel);

            //残カット
            if (loopClock != -1 && waitCounter > 0 && waitCounter != long.MaxValue)
            {
                lClock -= waitCounter;
                dSample -= (long)(waitCounter * (256 - info.timerB) * 1152.0 / (7987200.0 / 2.0) * 44100.0);
            }

            log.Write("フッター情報の作成");
            MakeFooter();

            return dat.ToArray();
        }

        public long GetWaitCounter(int ml)
        {
            //return (long)(ml * (256 - info.timerB) * 1152.0 / (7987200.0 / 2.0) * 44100.0);
            return ml;
        }

        private void partWorkByteData(partWork pw)
        {

            //未使用のパートの場合は処理を行わない
            if (!pw.chip.use) return;
            if (pw.mmlData == null) return;

            log.Write("MD stream pcm sound off");
            if (pw.pcmWaitKeyOnCounter == 0)
                pw.pcmWaitKeyOnCounter = -1;

            log.Write("KeyOff");
            ProcKeyOff(pw);

            log.Write("Bend");
            ProcBend(pw);

            log.Write("Lfo");
            ProcLfo(pw);

            log.Write("Envelope");
            ProcEnvelope(pw);

            pw.chip.SetFNum(pw);
            pw.chip.SetVolume(pw);

            log.Write("wait消化待ち");
            if (pw.waitCounter > 0)
            {
                return;
            }

            log.Write("データは最後まで実施されたか");
            if (pw.dataEnd)
            {
                return;
            }

            log.Write("パートのデータがない場合は何もしないで次へ");
            if (pw.mmlData == null || pw.mmlData.Count < 1)
            {
                pw.dataEnd = true;
                return;
            }

            log.Write("コマンド毎の処理を実施");
            while (pw.waitCounter == 0 && !pw.dataEnd)
            {
                if (pw.mmlPos >= pw.mmlData.Count)
                {
                    pw.dataEnd = true;
                }
                else
                {
                    MML mml = pw.mmlData[pw.mmlPos];
                    //lineNumber = pw.getLineNumber();
                    Commander(pw, mml);
                }
            }

        }

        private void MakeHeader()
        {

            //Header
            OutData(Const.hDat);

            //PCM Data block
            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                foreach (ClsChip chip in kvp.Value)
                {
                    chip.SetPCMDataBlock();
                }
            }

            //Set Initialize data
            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                foreach (ClsChip chip in kvp.Value)
                {
                    chip.InitChip();
                }
            }

        }

        private void MakeFooter()
        {

            byte[] v;

            //end of data
            OutData(0x66);

            //GD3 offset
            v = DivInt2ByteAry(dat.Count - 0x14);
            dat[0x14] = v[0]; dat[0x15] = v[1]; dat[0x16] = v[2]; dat[0x17] = v[3];

            //Total # samples
            v = DivInt2ByteAry((int)dSample);
            dat[0x18] = v[0]; dat[0x19] = v[1]; dat[0x1a] = v[2]; dat[0x1b] = v[3];

            if (loopOffset != -1)
            {
                //Loop offset
                v = DivInt2ByteAry((int)(loopOffset - 0x1c));
                dat[0x1c] = v[0]; dat[0x1d] = v[1]; dat[0x1e] = v[2]; dat[0x1f] = v[3];

                //Loop # samples
                v = DivInt2ByteAry((int)(dSample - loopSamples));
                dat[0x20] = v[0]; dat[0x21] = v[1]; dat[0x22] = v[2]; dat[0x23] = v[3];
            }

            int p = dat.Count + 12;

            MakeGD3(dat);

            //EoF offset
            v = DivInt2ByteAry(dat.Count - 0x4);
            dat[0x4] = v[0]; dat[0x5] = v[1]; dat[0x6] = v[2]; dat[0x7] = v[3];

            int q = dat.Count - p;

            //GD3 Length
            v = DivInt2ByteAry(q);
            dat[p - 4] = v[0]; dat[p - 3] = v[1]; dat[p - 2] = v[2]; dat[p - 1] = v[3];

            long useYM2151 = 0;
            long useYM2203 = 0;
            long useYM2608 = 0;
            long useYM2610B = 0;
            long useYM2612 = 0;
            long useSN76489 = 0;
            long useRf5c164 = 0;
            long useSegaPcm = 0;
            long useHuC6280 = 0;
            long useC140 = 0;
            long useAY8910 = 0;
            long useYM2413 = 0;
            long useK051649 = 0;

                foreach (partWork pw in ym2612[0].lstPartWork)
                { useYM2612 += pw.clockCounter; }
                foreach (partWork pw in sn76489[0].lstPartWork)
                { useSN76489 += pw.clockCounter; }

            if (useSN76489 == 0)
            { dat[0x0c] = 0; dat[0x0d] = 0; dat[0x0e] = 0; dat[0x0f] = 0; }
            if (useYM2612 == 0)
            { dat[0x2c] = 0; dat[0x2d] = 0; dat[0x2e] = 0; dat[0x2f] = 0; }
            if (useYM2151 == 0)
            { dat[0x30] = 0; dat[0x31] = 0; dat[0x32] = 0; dat[0x33] = 0; }
            if (useSegaPcm == 0)
            { dat[0x38] = 0; dat[0x39] = 0; dat[0x3a] = 0; dat[0x3b] = 0; dat[0x3c] = 0; dat[0x3d] = 0; dat[0x3e] = 0; dat[0x3f] = 0; }
            if (useYM2203 == 0)
            { dat[0x44] = 0; dat[0x45] = 0; dat[0x46] = 0; dat[0x47] = 0; }
            if (useYM2608 == 0)
            { dat[0x48] = 0; dat[0x49] = 0; dat[0x4a] = 0; dat[0x4b] = 0; }
            if (useYM2610B == 0)
            { dat[0x4c] = 0; dat[0x4d] = 0; dat[0x4e] = 0; dat[0x4f] = 0; }
            if (useRf5c164 == 0)
            { dat[0x6c] = 0; dat[0x6d] = 0; dat[0x6e] = 0; dat[0x6f] = 0; }
            if (useHuC6280 == 0)
            { dat[0xa4] = 0; dat[0xa5] = 0; dat[0xa6] = 0; dat[0xa7] = 0; }
            if (useC140 == 0)
            {
                dat[0xa8] = 0; dat[0xa9] = 0; dat[0xaa] = 0; dat[0xab] = 0;
                dat[0x96] = 0;
            }
            if (useAY8910 == 0)
            { dat[0x74] = 0; dat[0x75] = 0; dat[0x76] = 0; dat[0x77] = 0; dat[0x78] = 0; dat[0x79] = 0; dat[0x7a] = 0; dat[0x7b] = 0; }
            if (useYM2413 == 0)
            { dat[0x10] = 0; dat[0x11] = 0; dat[0x12] = 0; dat[0x13] = 0; }
            if (useK051649 == 0)
            { dat[0x9c] = 0; dat[0x9d] = 0; dat[0x9e] = 0; dat[0x9f] = 0; }

            if (info.Version == 1.51f)
            { dat[0x08] = 0x51; dat[0x09] = 0x01; }
            else if (info.Version == 1.60f)
            { dat[0x08] = 0x60; dat[0x09] = 0x01; }
            else
            { dat[0x08] = 0x61; dat[0x09] = 0x01; }

        }

        private void MakeGD3(List<byte> dat)
        {
            //'Gd3 '
            dat.Add(0x47); dat.Add(0x64); dat.Add(0x33); dat.Add(0x20);

            //GD3 Version
            dat.Add(0x00); dat.Add(0x01); dat.Add(0x00); dat.Add(0x00);

            //GD3 Length(dummy)
            dat.Add(0x00); dat.Add(0x00); dat.Add(0x00); dat.Add(0x00);

            //TrackName
            dat.AddRange(Encoding.Unicode.GetBytes(info.TitleName));
            dat.Add(0x00); dat.Add(0x00);
            dat.AddRange(Encoding.Unicode.GetBytes(info.TitleNameJ));
            dat.Add(0x00); dat.Add(0x00);

            //GameName
            dat.AddRange(Encoding.Unicode.GetBytes(info.GameName));
            dat.Add(0x00); dat.Add(0x00);
            dat.AddRange(Encoding.Unicode.GetBytes(info.GameNameJ));
            dat.Add(0x00); dat.Add(0x00);

            //SystemName
            dat.AddRange(Encoding.Unicode.GetBytes(info.SystemName));
            dat.Add(0x00); dat.Add(0x00);
            dat.AddRange(Encoding.Unicode.GetBytes(info.SystemNameJ));
            dat.Add(0x00); dat.Add(0x00);

            //Composer
            dat.AddRange(Encoding.Unicode.GetBytes(info.Composer));
            dat.Add(0x00); dat.Add(0x00);
            dat.AddRange(Encoding.Unicode.GetBytes(info.ComposerJ));
            dat.Add(0x00); dat.Add(0x00);

            //ReleaseDate
            dat.AddRange(Encoding.Unicode.GetBytes(info.ReleaseDate));
            dat.Add(0x00); dat.Add(0x00);

            //Converted
            dat.AddRange(Encoding.Unicode.GetBytes(info.Converted));
            dat.Add(0x00); dat.Add(0x00);

            //Notes
            dat.AddRange(Encoding.Unicode.GetBytes(info.Notes));
            dat.Add(0x00); dat.Add(0x00);

            //歌詞
            if (lyric != "")
            {
                dat.AddRange(Encoding.Unicode.GetBytes(lyric));
                dat.Add(0x00); dat.Add(0x00);
            }
        }

        private void ProcKeyOff(partWork pw)
        {
            if (pw.waitKeyOnCounter == 0)
            {
                if (!pw.tie)
                {
                    if (!pw.envelopeMode)
                    {
                        if (!pw.ReverbSwitch)
                        {
                            pw.chip.SetKeyOff(pw);
                        }
                        else
                        {
                            pw.ReverbNowSwitch = true;
                            pw.chip.SetVolume(pw);
                        }
                    }
                    else
                    {
                        if (!pw.ReverbSwitch)
                        {
                            if (pw.envIndex != -1)
                            {
                                pw.envIndex = 3;//RR phase
                            }
                        }
                        else
                        {
                            pw.envIndex = -1;
                            pw.ReverbNowSwitch = true;
                            pw.chip.SetVolume(pw);
                        }
                    }
                }

                //次回に引き継ぎリセット
                pw.beforeTie = pw.tie;
                //pw.tie = false;

                //ゲートタイムカウンターをリセット
                pw.waitKeyOnCounter = -1;
            }
        }

        private void ProcBend(partWork pw)
        {
            //bend処理
            if (pw.bendWaitCounter == 0)
            {
                if (pw.bendList.Count > 0)
                {
                    Tuple<int, int> bp = pw.bendList.Pop();
                    pw.bendFnum = bp.Item1;
                    pw.bendWaitCounter = GetWaitCounter(bp.Item2);
                }
                else
                {
                    pw.bendWaitCounter = -1;
                }
            }
        }

        private void ProcLfo(partWork cpw)
        {
            //lfo処理
            for (int lfo = 0; lfo < 1; lfo++)
            {
                clsLfo pl = cpw.lfo[lfo];

                if (!pl.sw)
                {
                    continue;
                }
                if (pl.waitCounter > 0)//== -1)
                {
                    continue;
                }

                pl.waitCounter = GetWaitCounter(pl.param[1]);

                if (pl.PeakLevelCounter == 0)
                {
                    pl.PeakLevelCounter = pl.param[3];
                    pl.direction = -pl.direction;
                }
                pl.PeakLevelCounter--;
                pl.value += Math.Abs(pl.param[2]) * pl.direction;

                //if (pl.type == eLfoType.Hardware)
                //{
                //    if (cpw.chip is YM2612)
                //    {
                //        cpw.ams = pl.param[3];
                //        cpw.fms = pl.param[2];
                //        ((ClsOPN)cpw.chip).OutOPNSetPanAMSPMS(cpw, (int)cpw.pan.val, cpw.ams, cpw.fms);
                //        cpw.chip.lstPartWork[0].hardLfoSw = true;
                //        cpw.chip.lstPartWork[0].hardLfoNum = pl.param[1];
                //        ((ClsOPN)cpw.chip).OutOPNSetHardLfo(cpw, cpw.hardLfoSw, cpw.hardLfoNum);
                //        pl.waitCounter = -1;
                //    }
                //    continue;
                //}

                //switch (pl.param[4])
                //{
                //    case 0: //三角
                //        pl.value += Math.Abs(pl.param[2]) * pl.direction;
                //        pl.waitCounter = pl.param[1];
                //        if ((pl.direction > 0 && pl.value >= pl.param[3]) || (pl.direction < 0 && pl.value <= -pl.param[3]))
                //        {
                //            pl.value = pl.param[3] * pl.direction;
                //            pl.direction = -pl.direction;
                //        }
                //        break;
                //    case 1: //のこぎり
                //        pl.value += Math.Abs(pl.param[2]) * pl.direction;
                //        pl.waitCounter = pl.param[1];
                //        if ((pl.direction > 0 && pl.value >= pl.param[3]) || (pl.direction < 0 && pl.value <= -pl.param[3]))
                //        {
                //            pl.value = -pl.param[3] * pl.direction;
                //        }
                //        break;
                //    case 2: //矩形
                //        if (pl.direction < 0) pl.value = pl.param[2];
                //        else pl.value = pl.param[3];
                //        pl.waitCounter = pl.param[1];
                //        pl.direction = -pl.direction;
                //        break;
                //    case 3: //ワンショット
                //        pl.value += Math.Abs(pl.param[2]) * pl.direction;
                //        pl.waitCounter = pl.param[1];
                //        if ((pl.direction > 0 && pl.value >= pl.param[3]) || (pl.direction < 0 && pl.value <= -pl.param[3]))
                //        {
                //            pl.waitCounter = -1;
                //        }
                //        break;
                //    case 4: //ランダム
                //        pl.value = rnd.Next(-pl.param[3], pl.param[3]);
                //        pl.waitCounter = pl.param[1];
                //        break;
                //}

            }
        }

        private void ProcEnvelope(partWork pw)
        {
            if (!pw.envelopeMode) return;
            if (pw.envIndex == -1) return;

            int maxValue = pw.MaxVolume;
            //while (pw.envCounter == 0 && pw.envIndex != -1)
            //{
                switch (pw.envIndex)
                {
                    case 0: //Attack phase
                        pw.envCounter += pw.envelope[1]; // counter += AR
                        if (pw.envCounter >= 255)
                        {
                            pw.envCounter = 255;
                            pw.envIndex++;
                        }
                        break;
                    case 1: //Decay phase
                        pw.envCounter -= pw.envelope[2]; // counter -= DR
                        if (pw.envCounter <= pw.envelope[3]) // counter <= SR
                        {
                            pw.envCounter = pw.envelope[3]; // SR
                            pw.envIndex++;
                        }
                        break;
                    case 2: //Sustain phase
                        pw.envCounter -= pw.envelope[4]; // counter -= SL
                        if (pw.envCounter <= 0)
                        {
                            pw.envCounter = 0;
                            pw.envIndex = -1;
                        }
                        break;
                    case 3: //Release phase
                        pw.envCounter -= pw.envelope[5]; // counter -= RR
                        if (pw.envCounter <= 0)
                        {
                            pw.envCounter = 0;
                            pw.envIndex = -1;
                        }
                        break;
                }
            //}

            if (pw.envIndex == -1)
            {
                pw.chip.SetKeyOff(pw);
            }
        }

        private void Commander(partWork pw, MML mml)
        {

            switch (mml.type)
            {
                case enmMMLType.Clock:
                    log.Write("Clock (C)");
                    pw.chip.CmdClock(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.TimerB:
                    log.Write("TimerB (t)");
                    pw.chip.CmdTimerB(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Tempo:
                    log.Write("Tempo (T)");
                    pw.chip.CmdTempo(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.CompileSkip:
                    log.Write("CompileSkip");
                    pw.dataEnd = true;
                    pw.waitCounter = -1;
                    break;
                case enmMMLType.Instrument:
                    log.Write("Instrument");
                    pw.chip.CmdInstrument(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Octave:
                    log.Write("Octave");
                    pw.chip.CmdOctave(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.OctaveUp:
                    log.Write("OctaveUp");
                    pw.chip.CmdOctaveUp(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.OctaveDown:
                    log.Write("OctaveDown");
                    pw.chip.CmdOctaveDown(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Length:
                    log.Write("Length");
                    pw.chip.CmdLength(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.LengthClock:
                    log.Write("LengthClock");
                    pw.chip.CmdClockLength(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.TotalVolume:
                    log.Write("TotalVolume");
                    pw.chip.CmdTotalVolume(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Volume:
                    log.Write("Volume");
                    pw.chip.CmdVolume(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.VolumeDown:
                    log.Write("VolumeDown");
                    pw.chip.CmdVolumeDown(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.VolumeUp:
                    log.Write("VolumeUp");
                    pw.chip.CmdVolumeUp(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Pan:
                    log.Write("Pan");
                    pw.chip.CmdPan(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Gatetime:
                    log.Write("Gatetime");
                    pw.chip.CmdGatetime(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.GatetimeDiv:
                    log.Write("GatetimeDiv");
                    pw.chip.CmdGatetime2(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Detune:
                    log.Write("Detune");
                    pw.chip.CmdDetune(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Renpu:
                    log.Write("Renpu");
                    pw.chip.CmdRenpuStart(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.RenpuEnd:
                    log.Write("RenpuEnd");
                    pw.chip.CmdRenpuEnd(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Repeat:
                    log.Write("Repeat");
                    pw.chip.CmdRepeatStart(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.RepeatEnd:
                    log.Write("RepeatEnd");
                    pw.chip.CmdRepeatEnd(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.RepertExit:
                    log.Write("RepertExit");
                    pw.chip.CmdRepeatExit(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Note:
                    log.Write("Note");
                    pw.chip.CmdNote(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Rest:
                    log.Write("Rest");
                    pw.chip.CmdRest(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Lyric:
                    log.Write("Lyric");
                    pw.chip.CmdLyric(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Envelope:
                    log.Write("Envelope");
                    pw.chip.CmdEnvelope(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.HardEnvelope:
                    log.Write("HardEnvelope");
                    pw.chip.CmdHardEnvelope(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.ExtendChannel:
                    log.Write("ExtendChannel");
                    pw.chip.CmdExtendChannel(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SoftLfo:
                    log.Write("SoftLfo");
                    pw.chip.CmdSoftLfo(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SoftLfoOnOff:
                    log.Write("SoftLfoOnOff");
                    pw.chip.CmdSoftLfoOnOff(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SoftLfoDelay:
                    log.Write("SoftLfoDelay");
                    pw.chip.CmdSoftLfoDelay(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SoftLfoClock:
                    log.Write("SoftLfoClock");
                    pw.chip.CmdSoftLfoClock(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SoftLfoDepth:
                    log.Write("SoftLfoDepth");
                    pw.chip.CmdSoftLfoDepth(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SoftLfoLength:
                    log.Write("SoftLfoLength");
                    pw.chip.CmdSoftLfoLength(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Lfo:
                    log.Write("Lfo");
                    pw.chip.CmdLfo(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.LfoSwitch:
                    log.Write("LfoSwitch");
                    pw.chip.CmdLfoSwitch(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.HardLfo:
                    log.Write("HardLfo");
                    pw.chip.CmdHardLfo(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.PcmMode:
                    log.Write("PcmMode");
                    pw.chip.CmdMode(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Noise:
                    log.Write("Noise");
                    pw.chip.CmdNoise(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Bend:
                    log.Write("Bend");
                    pw.chip.CmdBend(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Y:
                    log.Write("Y");
                    pw.chip.CmdY(pw, mml);
                    pw.mmlPos++;

                    if(pw.chip is YM2612)
                    {
                        if (mml.args[0] is byte && (byte)mml.args[0] == 0x26)
                        {
                            info.timerB = (byte)mml.args[1];
                        }
                    }
                    break;
                case enmMMLType.LoopPoint:
                    log.Write("LoopPoint");
                    pw.chip.CmdLoop(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Jump:
                    useJumpCommand--;
                    if (useJumpCommand < 0) useJumpCommand = 0;
                    pw.mmlPos++;
                    break;
                case enmMMLType.MixerMode:
                    log.Write("NoiseToneMixer");
                    pw.chip.CmdNoiseToneMixer(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.KeyShift:
                    log.Write("KeyShift");
                    pw.chip.CmdKeyShift(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.RelativeKeyShift:
                    log.Write("RelativeKeyShift");
                    pw.chip.CmdRelKeyShift(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.RelativeVolume:
                    log.Write("RelativeVolume");
                    pw.chip.CmdRelativeVolume(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SusOnOff:
                    log.Write("SusOnOff");
                    pw.chip.CmdSusOnOff(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Porta:
                    log.Write("Porta");
                    pw.mmlPos++;
                    break;
                case enmMMLType.PortaEnd:
                    log.Write("PortaEnd");
                    pw.mmlPos++;
                    break;
                case enmMMLType.Reverb:
                    log.Write("Reverb");
                    pw.chip.CmdReverb(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.ReverbONOF:
                    log.Write("ReverbONOF");
                    pw.chip.CmdReverbONOF(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.ReverbMode:
                    log.Write("ReverbMode");
                    pw.chip.CmdReverbMode(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.SlotDetune:
                    log.Write("SlotDetune");
                    pw.chip.CmdSlotDetune(pw, mml);
                    pw.mmlPos++;
                    break;
                case enmMMLType.Shuffle:
                    log.Write("Shuffle");
                    pw.mmlPos++;
                    break;
                default:
                    msgBox.setErrMsg(string.Format(msg.get("E01016")
                        , mml.type)
                        , mml.line.Fn
                        , mml.line.Num);
                    pw.mmlPos++;
                    break;
            }
        }


        public void PartInit()
        {
            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                foreach (ClsChip chip in kvp.Value)
                {
                    chip.use = false;
                    chip.lstPartWork = new List<partWork>();

                    for (int i = 0; i < chip.ChMax; i++)
                    {
                        partWork pw = new partWork()
                        {
                            chip = chip,
                            isSecondary = (chip.ChipID == 1),
                            ch = i// + 1;
                        };

                        if (partData.ContainsKey(chip.Ch[i].Name))
                        {
                            pw.pData = partData[chip.Ch[i].Name];
                        }
                        pw.aData = aliesData;
                        pw.setPos(0);

                        pw.Type = chip.Ch[i].Type;
                        pw.slots = 0;
                        pw.volume = 32767;

                        chip.InitPart(ref pw);

                        pw.PartName = chip.Ch[i].Name;
                        pw.waitKeyOnCounter = -1;
                        pw.waitCounter = 0;
                        pw.freq = -1;

                        pw.dataEnd = false;
                        if (pw.pData == null || pw.pData.Count < 1)
                        {
                            pw.dataEnd = true;
                        }
                        else
                        {
                            chip.use = true;
                        }

                        chip.lstPartWork.Add(pw);

                    }
                }
            }
        }

        public void SetMMLDataToPart(Dictionary<string, List<MML>> mmlData)
        {
            if (mmlData == null) return;

            foreach (KeyValuePair<enmChipType, ClsChip[]> kvp in chips)
            {
                foreach (ClsChip chip in kvp.Value)
                {
                    foreach (partWork pw in chip.lstPartWork)
                    {
                        pw.pData = null;
                        pw.aData = null;
                        //pw.mmlData = null;
                        pw.dataEnd = true;
                        if (mmlData.ContainsKey(pw.PartName))
                        {
                            //pw.mmlData = mmlData[pw.PartName];
                            chip.use = true;
                            pw.dataEnd = false;
                        }
                    }
                }
            }
        }

        private byte[] DivInt2ByteAry(int n)
        {
            return new byte[4] {
                 (byte)( n & 0xff                   )
                ,(byte)((n & 0xff00    ) / 0x100    )
                ,(byte)((n & 0xff0000  ) / 0x10000  )
                ,(byte)((n & 0xff000000) / 0x1000000)
            };
        }

        public void OutData(params byte[] data)
        {
            foreach (byte d in data) dat.Add(d);
        }

        private void OutWaitNSamples(long n)
        {
            long m = n;

            while (m > 0)
            {
                if (m > 0xffff)
                {
                    OutData(
                        0x61
                        , (byte)0xff
                        , (byte)0xff
                        );
                    m -= 0xffff;
                }
                else
                {
                    OutData(
                        0x61
                        , (byte)(m & 0xff)
                        , (byte)((m & 0xff00) >> 8)
                        );
                    m = 0L;
                }
            }
        }

        private void OutWait735Samples(int repeatCount)
        {
            for (int i = 0; i < repeatCount; i++)
            {
                OutData(0x62);
            }
        }

        private void OutWait882Samples(int repeatCount)
        {
            for (int i = 0; i < repeatCount; i++)
            {
                OutData(0x63);
            }
        }

        private void OutWaitNSamplesWithPCMSending(partWork cpw, long cnt)
        {
            //for (int i = 0; i < cpw.samplesPerClock * cnt;)
            //{

            //    int f = (int)cpw.pcmBaseFreqPerFreq;
            //    cpw.pcmFreqCountBuffer += cpw.pcmBaseFreqPerFreq - (int)cpw.pcmBaseFreqPerFreq;
            //    while (cpw.pcmFreqCountBuffer > 1.0f)
            //    {
            //        f++;
            //        cpw.pcmFreqCountBuffer -= 1.0f;
            //    }
            //    if (i + f >= cpw.samplesPerClock * cnt)
            //    {
            //        cpw.pcmFreqCountBuffer += (int)(i + f - cpw.samplesPerClock * cnt);
            //        f = (int)(cpw.samplesPerClock * cnt - i);
            //    }
            //    if (cpw.pcmSizeCounter > 0)
            //    {
            //        cpw.pcmSizeCounter--;
            //        OutData((byte)(0x80 + f));
            //    }
            //    else
            //    {
            //        OutWaitNSamples(f);
            //    }
            //    i += f;
            //}
        }


    }
}
