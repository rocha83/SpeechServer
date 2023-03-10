using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using Microsoft.Speech.Recognition;
using System.Globalization;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace Rochas.CacheServer
{
    public static class SpeechEngine
    {
        static SpeechRecognitionEngine spcEngine = null;
        static Grammar grammar = null;
        static bool audioReaded = false;
        static string callGuid = string.Empty;

        private static string RecognizeWord(bool sentenceSender = false)
        {
            string result = string.Empty;

            if (grammar != null && (audioReaded || sentenceSender))
            {
                RecognitionResult recogResult = spcEngine.Recognize();

                if (recogResult != null)
                    result = recogResult.Text;
                else
                    result = string.Empty;
            }
            else
                throw new ArgumentNullException("First initialize the grammar list and set the audio buffer.");

            return result;
        }

        public static void Init(string[] grammarChoices, string culture)
        {
            spcEngine = new SpeechRecognitionEngine(new CultureInfo(culture));

            Choices realObjects = new Choices();
            realObjects.Add(grammarChoices.Take(13000).ToArray());
            //realObjects.Add(new string[] { "mesa", "cadeira", "copo", "panela", "veiculo", "predio", "andar", "setor", "tatiana", "maria", "renato", "computador" });

            GrammarBuilder grammarBuilder = new GrammarBuilder();
            grammarBuilder.Culture = new CultureInfo(culture);
            grammarBuilder.Append(realObjects);

            grammar = new Grammar(grammarBuilder);

            try
            {
                spcEngine.LoadGrammar(grammar);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        public static void SplitAudioWords(Stream audioBuffer)
        {
            callGuid = Guid.NewGuid().ToString();

            try
            {
                string dumpPath = ConfigurationManager.AppSettings["DumpPath"];
                string soxPath = ConfigurationManager.AppSettings["SoxPath"];
                //string soxNoiseRdArgs = ConfigurationManager.AppSettings["SoxNoiseRdArgs"];
                string soxCpndArgs = ConfigurationManager.AppSettings["SoxCpndArgs"];
                string soxSplitArgs = ConfigurationManager.AppSettings["SoxSplitArgs"];

                string soxCmd = string.Concat(soxPath, "\\sox.exe");
                //soxNoiseRdArgs = string.Format(soxNoiseRdArgs, callGuid, soxPath);
                soxCpndArgs = string.Format(soxCpndArgs, callGuid);
                soxSplitArgs = string.Format(soxSplitArgs, callGuid);

                if ((dumpPath != null) && (soxPath != null))
                {
                    string wavFile = string.Concat(dumpPath, "\\audiobuffer", callGuid ,".wav");
                    FileStream wavFileStream = new FileStream(wavFile, FileMode.CreateNew);
                    
                    audioBuffer.Position = 0;
                    audioBuffer.CopyTo(wavFileStream);

                    wavFileStream.Close();
                    wavFileStream.Dispose();

                    //Process.Start(soxCmd, soxNoiseRdArgs);
                    //Thread.Sleep(300);
                    Process.Start(soxCmd, soxCpndArgs);
                    Thread.Sleep(200);
                    Process.Start(soxCmd, soxSplitArgs);
                    Thread.Sleep(200);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static string[] RecognizeSentence()
        {
            List<string> result = new List<string>(); ;

            try
            {
                string dumpPath = ConfigurationManager.AppSettings["DumpPath"];

                string audioOutPattern = string.Format("audioword{0}*.wav", callGuid);
                string[] wordFiles = Directory.GetFiles(dumpPath, string.Format(audioOutPattern, callGuid));

                foreach (var wordFile in wordFiles)
                {
                    spcEngine.SetInputToWaveFile(wordFile);

                    string recogResult = RecognizeWord(true);

                    if (!string.IsNullOrEmpty(recogResult))
                        result.Add(recogResult);

                    spcEngine.SetInputToNull();
                }

                foreach (var wordFile in wordFiles)
                    if (File.Exists(string.Concat(wordFile)))
                        File.Delete(string.Concat(wordFile));

                string srcCmpndAudioPath = string.Concat(dumpPath, string.Format("\\companedaudio{0}.wav", callGuid));
                if (File.Exists(srcCmpndAudioPath))
                    File.Delete(srcCmpndAudioPath);

                string srcClndAudioPath = string.Concat(dumpPath, string.Format("\\cleanedaudio{0}.wav", callGuid));
                if (File.Exists(srcClndAudioPath))
                    File.Delete(srcClndAudioPath);

                string srcAudioPath = string.Concat(dumpPath, string.Format("\\audiobuffer{0}.wav", callGuid));
                if (File.Exists(srcAudioPath))
                    File.Delete(srcAudioPath);

                return result.ToArray();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
