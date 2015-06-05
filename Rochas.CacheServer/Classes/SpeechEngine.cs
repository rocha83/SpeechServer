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
            realObjects.Add(grammarChoices.Take(12000).ToArray());
            //new string[] { "copo", "panela", "veiculo", "imovel", "setor", "andar" }

            GrammarBuilder grammarBuilder = new GrammarBuilder();
            grammarBuilder.Culture = new CultureInfo(culture);
            grammarBuilder.Append(realObjects);

            grammar = new Grammar(grammarBuilder);

            spcEngine.LoadGrammar(grammar);
        }

        public static void SplitAudioWords(Stream audioBuffer)
        {
            callGuid = Guid.NewGuid().ToString();

            try
            {
                string dumpPath = ConfigurationManager.AppSettings["DumpPath"];
                string soxPath = ConfigurationManager.AppSettings["SoxPath"];
                string soxArgs = ConfigurationManager.AppSettings["SoxArgs"];

                string soxCmd = string.Concat(soxPath, "\\sox.exe");
                soxArgs = string.Format(soxArgs, callGuid);

                if ((dumpPath != null) && (soxPath != null))
                {
                    string wavFile = string.Concat(dumpPath, "\\audiobuffer", callGuid ,".wav");
                    FileStream wavFileStream = new FileStream(wavFile, FileMode.CreateNew);
                    
                    audioBuffer.Position = 0;
                    audioBuffer.CopyTo(wavFileStream);

                    wavFileStream.Close();
                    wavFileStream.Dispose();

                    Process.Start(soxCmd, soxArgs);
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

            Thread.Sleep(200);

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
