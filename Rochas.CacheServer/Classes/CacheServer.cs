using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using System.Reflection;
using System.Web;
using System.IO;
using System.Configuration;
using System.Text;
using System.Collections;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Xml;

namespace Rochas.CacheServer
{
    public static class CacheServer
    {
        #region Declarations

        // Dicionário para os objetos previamente identificados e efetivamente em cache
        private static IDictionary<string, IDictionary<long, object>> cacheItems = new SortedDictionary<string, IDictionary<long, object>>();

        // Dicionário os índices aos identificadores dos objetos em cache conforme o tipo de dado
        private static IDictionary<string, IDictionary<double, IList<long>>> numericCacheIndex = new SortedDictionary<string, IDictionary<double, IList<long>>>();
        private static IDictionary<string, IDictionary<long, IList<long>>> timeCacheIndex = new SortedDictionary<string, IDictionary<long, IList<long>>>();
        private static IDictionary<string, IDictionary<string, IList<long>>> textCacheIndex = new SortedDictionary<string, IDictionary<string, IList<long>>>();

        // Instância do modelo a ser utilizado nas listas de cache
        private static SortedDictionary<string, byte[]> asmBufferInstances = new SortedDictionary<string, byte[]>();
        private static SortedDictionary<string, object> modelInstances = new SortedDictionary<string, object>();

        #endregion

        #region Public Methods

        public static string GetJson(string filter, string className = "", string selection = "")
        {
            return JsonConvert.SerializeObject(GetList(filter, className, selection));
        }

        public static IEnumerable GetList(string filter, string className, string selection = "")
        {
            double fakeDoub;
            DateTime fakeDate;
            IEnumerable<long> foundedKeys = new List<long>();
            IEnumerable result = new List<object>();
            List<long> foundedMultiKeys = new List<long>();
            List<object> multiResult = new List<object>();

            if (!string.IsNullOrEmpty(selection) && !modelInstances.ContainsKey(className))
                throw new TypeInitializationException("Model name not found. Please enter the correct model name to select the attributes.", null);

            filter = filter.ToLower();
            className = className.ToLower();

            try
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    bool numericFilter = double.TryParse(filter, out fakeDoub);
                    bool timeFilter = DateTime.TryParse(filter, out fakeDate);

                    if (numericFilter)
                    {
                        result = getNumIndexValues(fakeDoub, foundedKeys, foundedMultiKeys, className);
                    }
                    else if (timeFilter)
                    {
                        result = getTimeIndexValues(fakeDate, foundedKeys, foundedMultiKeys, className);
                    }
                    else
                    {
                        if (filter.Contains('|'))
                            result = deepGet(filter, className);
                        else
                        {
                            if (string.IsNullOrEmpty(className))
                            {
                                foreach (var textCache in textCacheIndex)
                                    if (textCache.Value.ContainsKey(filter))
                                        foundedMultiKeys.AddRange(textCache.Value[filter]);

                                foreach (var cacheItem in cacheItems)
                                    multiResult.AddRange(cacheItem.Value.Where(cci => foundedMultiKeys.Contains(cci.Key)).Select(cci => cci.Value));

                                result = multiResult;
                            }
                            else
                            {
                                textCacheIndex[className].ContainsKey(filter);
                                foundedKeys = textCacheIndex[className][filter];
                                result = cacheItems[className].Where(cci => foundedKeys.Contains(cci.Key)).Select(cci => cci.Value);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(className))
                    {
                        if (!string.IsNullOrEmpty(selection))
                        {
                            Type modelType = modelInstances[className].GetType();

                            if (modelType != null)
                            {
                                var typedResult = Activator.CreateInstance(Reflector.GetTypedCollection(modelType)) as IList;

                                foreach (var resultItem in result)
                                    typedResult.Add(resultItem);

                                result = typedResult.Select(mountSelectionQuery(selection));
                            }
                        }
                    }
                }
                else
                    result = getAll(className);

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return result;
        }

        public static string[] GetSpeech(Stream audioBuffer)
        {
            SpeechEngine.SplitAudioWords(audioBuffer);

            return SpeechEngine.RecognizeSentence();
        }

        public static void Post(byte[] asmBuffer, string dataSource, string className, bool replaceInstance = false)
        {
            Type modelType = null;
            object dataSourceInstance = null;

            try
            {
                // Carregando instância do modelo informado  na lista de instâncias
                object modelInstance = Assembly.Load(asmBuffer).CreateInstance(className);
                
                className = className.ToLower();

                // Carregando buffer do Assembly para o modelo informado na lista de buffers
                if (asmBuffer != null)
                {
                    if (!asmBufferInstances.ContainsKey(className))
                        asmBufferInstances.Add(className, asmBuffer);
                    else
                    {
                        if (replaceInstance)
                            asmBufferInstances[className] = asmBuffer;
                    }
                }

                if (modelInstance != null)
                {
                    modelType = modelInstance.GetType();

                    if (!modelInstances.ContainsKey(className))
                        modelInstances.Add(className, modelInstance);
                    else
                        if (replaceInstance)
                            modelInstances[className] = modelInstance;
                }

                // Obtendo propriedades conforme seu tipo de dado
                var objPropGrp = getItemProps(modelInstance);

                // Carregando fonte de dados informada na lista de fontes de dados
                dataSourceInstance = JsonConvert.DeserializeObject(dataSource, Reflector.GetTypedCollection(modelType));
                
                // Inicializa a lista de items para o modelo informado, caso vazia
                if (((IEnumerable)dataSourceInstance).Any())
                    if (!cacheItems.ContainsKey(className))
                        cacheItems.Add(className, new ConcurrentDictionary<long, object>());

                foreach (var dataItem in (IEnumerable)dataSourceInstance)
                {
                    // Identificador do objeto no dicionário cache
                    int itemId = dataItem.GetHashCode();

                    // Adiciona ou atualiza objeto no dicionário cache
                    //if (cacheItems[className].ContaisKey(itemId))
                    //    cacheItems[className][itemId] = dataItem;
                    //else

                    if (!cacheItems[className].ContainsKey(itemId))
                        cacheItems[className].Add(itemId, dataItem);

                    // Distribui os dados nos indíces conforme o respectivo tipo de dado
                    distribIndexData(className, objPropGrp, dataItem, itemId);
                }

                // Alimenta o dicionário gramatical para interpretação das palavras por vor
                List<string> wordList = new List<string>();
                foreach (var textCache in textCacheIndex.Values)
                    wordList.AddRange(textCache.Keys);
                
                SpeechEngine.Init(wordList.ToArray(), "pt-BR");

                GC.Collect();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static string GetMemoryUsage()
        {
            var memSize = GC.GetTotalMemory(false) / 1024 / 1024;

            return string.Format("The service memory size is {0} MB", memSize.ToString());
        }

        public static void Save(string className = "", bool enableCompression = false)
        {
            string dumpPath = ConfigurationManager.AppSettings["dumpPath"];
            if (dumpPath != null)
            {
                try
                {
                    byte[] asmDump = null;
                    string contentDump = string.Empty;
                    string numericIndexDump = string.Empty;
                    string timeIndexDump = string.Empty;
                    string textIndexDump = string.Empty;

                    if (!string.IsNullOrEmpty(className))
                    {
                        className = className.ToLower();
                        asmDump = asmBufferInstances[className];
                        contentDump = JsonConvert.SerializeObject(cacheItems[className]);
                        numericIndexDump = JsonConvert.SerializeObject(numericCacheIndex[className]);
                        timeIndexDump = JsonConvert.SerializeObject(timeCacheIndex[className]);
                        textIndexDump = JsonConvert.SerializeObject(textCacheIndex[className]);
                    }
                    else
                    {
                        asmDump = Serializer.SerializeBinary(asmBufferInstances);
                        contentDump = JsonConvert.SerializeObject(cacheItems);
                        numericIndexDump = JsonConvert.SerializeObject(numericCacheIndex);
                        timeIndexDump = JsonConvert.SerializeObject(timeCacheIndex);
                        textIndexDump = JsonConvert.SerializeObject(textCacheIndex);
                    }

                    if (enableCompression)
                    {
                        contentDump = Compressor.ZipText(contentDump);
                        numericIndexDump = Compressor.ZipText(numericIndexDump);
                        timeIndexDump = Compressor.ZipText(timeIndexDump);
                        textIndexDump = Compressor.ZipText(textIndexDump);
                    }

                    File.WriteAllBytes(string.Concat(dumpPath, "\\", className, "AsmBufferDump.dat"), asmDump);
                    File.WriteAllText(string.Concat(dumpPath, "\\", className, "CacheContentDump.dat"), contentDump);
                    File.WriteAllText(string.Concat(dumpPath, "\\", className, "NumericIndexDump.dat"), numericIndexDump);
                    File.WriteAllText(string.Concat(dumpPath, "\\", className, "TimeIndexDump.dat"), timeIndexDump);
                    File.WriteAllText(string.Concat(dumpPath, "\\", className, "TextIndexDump.dat"), textIndexDump);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            else
                throw new ConfigurationException("Storage path not defined.");
        }

        public static void Load(string className = "", bool enableCompression = false)
        {
            string dumpPath = ConfigurationManager.AppSettings["dumpPath"];
            if (dumpPath != null)
            {
                byte[] asmRawFormat = null;
                string contentDump = string.Empty;
                string numericIndexDump = string.Empty;
                string timeIndexDump = string.Empty;
                string textIndexDump = string.Empty;

                try
                {
                    asmRawFormat = File.ReadAllBytes(string.Concat(dumpPath, "\\", className, "AsmBufferDump.dat"));
                    contentDump = File.ReadAllText(string.Concat(dumpPath, "\\", className, "CacheContentDump.dat"));
                    numericIndexDump = File.ReadAllText(string.Concat(dumpPath, "\\", className, "NumericIndexDump.dat"));
                    timeIndexDump = File.ReadAllText(string.Concat(dumpPath, "\\", className, "TimeIndexDump.dat"));
                    textIndexDump = File.ReadAllText(string.Concat(dumpPath, "\\", className, "TextIndexDump.dat"));

                    if (enableCompression)
                    {
                        contentDump = Compressor.UnZipText(contentDump);
                        numericIndexDump = Compressor.UnZipText(numericIndexDump);
                        timeIndexDump = Compressor.UnZipText(timeIndexDump);
                        textIndexDump = Compressor.UnZipText(textIndexDump);
                    }

                    if (!string.IsNullOrEmpty(className))
                    {
                        if (!asmBufferInstances.ContainsKey(className))
                            asmBufferInstances.Add(className, null);
                        asmBufferInstances[className] = asmRawFormat;

                        if (!cacheItems.ContainsKey(className))
                            cacheItems.Add(className, null);
                        cacheItems[className] = JsonConvert.DeserializeObject<ConcurrentDictionary<long, object>>(contentDump);

                        numericCacheIndex[className] = JsonConvert.DeserializeObject<IDictionary<double, IList<long>>>(numericIndexDump);
                        timeCacheIndex[className] = JsonConvert.DeserializeObject<IDictionary<long, IList<long>>>(timeIndexDump);
                        textCacheIndex[className] = JsonConvert.DeserializeObject<IDictionary<string, IList<long>>>(textIndexDump);
                    }
                    else
                    {
                        asmBufferInstances = Serializer.DeserializeBinary(asmRawFormat) as SortedDictionary<string, byte[]>;
                        cacheItems = JsonConvert.DeserializeObject<SortedDictionary<string, IDictionary<long, object>>>(contentDump);
                        numericCacheIndex = JsonConvert.DeserializeObject<SortedDictionary<string, IDictionary<double, IList<long>>>>(numericIndexDump);
                        timeCacheIndex = JsonConvert.DeserializeObject<SortedDictionary<string, IDictionary<long, IList<long>>>>(timeIndexDump);
                        textCacheIndex = JsonConvert.DeserializeObject<SortedDictionary<string, IDictionary<string, IList<long>>>>(textIndexDump);
                    }

                    modelInstances = new SortedDictionary<string, object>();

                    if (asmBufferInstances != null)
                        foreach (var asmBuff in asmBufferInstances)
                        {
                            Assembly asmInstance = Assembly.Load(asmBuff.Value);
                            modelInstances.Add(asmBuff.Key, asmInstance.CreateInstance(asmBuff.Key));
                        }

                    List<string> wordList = new List<string>();
                    foreach (var textCache in textCacheIndex.Values)
                        wordList.AddRange(textCache.Keys);

                    SpeechEngine.Init(wordList.ToArray(), "pt-BR");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            else
                throw new ConfigurationException("Storage path not defined.");
        }

        public static void Reset()
        {
            asmBufferInstances.Clear();
            modelInstances.Clear();
            cacheItems.Clear();
            numericCacheIndex.Clear();
            timeCacheIndex.Clear();
            textCacheIndex.Clear();

            cacheItems.Clear();
            numericCacheIndex.Clear();
            timeCacheIndex.Clear();
            textCacheIndex.Clear();

            GC.Collect();
        }

        public static string ConvertXml(string xml)
        {
            string result = string.Empty;
            XmlDocument xmlDoc = new XmlDocument();

            try
            {
                xmlDoc.LoadXml(xml);

                result = JsonConvert.SerializeXmlNode(xmlDoc);

                result = result.Substring(result.IndexOf('['));
                result = result.Substring(0, result.Length - 2);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return result;
        }

        #endregion

        #region Private Methods

        private static IDictionary<Type, IEnumerable<PropertyInfo>> getItemProps(object cacheItem)
        {
            Dictionary<Type, IEnumerable<PropertyInfo>> result = new Dictionary<Type, IEnumerable<PropertyInfo>>();

            // Obtendo valores numéricos para inclusão no devido índice
            List<PropertyInfo> numericProps = Reflector.getObjectProps(cacheItem, typeof(short)).ToList();
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(short?)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(int)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(int?)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(long)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(long?)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(decimal)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(decimal?)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(float)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(float?)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(double)).ToList());
            numericProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(double?)).ToList());
            result.Add(typeof(double), numericProps);

            // Obtendo valores data e hora para inclusão no devido índice
            List<PropertyInfo> timeProps = Reflector.getObjectProps(cacheItem, typeof(DateTime)).ToList();
            timeProps.AddRange(Reflector.getObjectProps(cacheItem, typeof(DateTime?)).ToList());
            result.Add(typeof(long), timeProps);

            // Obtendo valores texto para inclusão no devido índice
            List<PropertyInfo> textProps = Reflector.getObjectProps(cacheItem, typeof(string)).ToList();
            result.Add(typeof(string), textProps);

            return result;
        }

        // Distribui os dados do objeto nas listas de índice conforme o tipo de dado
        private static void distribIndexData(string className, IDictionary<Type, IEnumerable<PropertyInfo>> propGroup, object cacheItem, long itemId)
        {
            object propInst = null;

            try
            {
                foreach (var propList in propGroup)
                    if (propList.Key.FullName.Equals("System.Double")) // Índice para números
                    {
                        foreach (var prop in propList.Value)
                        {
                            propInst = prop.GetValue(cacheItem, null);

                            if (propInst != null)
                            {
                                double numValue = double.Parse(propInst.ToString());
                                IList<long> cacheItemRefs = new List<long>();

                                if (!numericCacheIndex.ContainsKey(className))
                                    numericCacheIndex.Add(className, new SortedDictionary<double, IList<long>>());

                                if (numericCacheIndex[className].ContainsKey(numValue))
                                {
                                    if (!numericCacheIndex[className][numValue].Contains(itemId))
                                        numericCacheIndex[className][numValue].Add(itemId);
                                }
                                else
                                {
                                    cacheItemRefs.Add(itemId);
                                    numericCacheIndex[className].Add(numValue, cacheItemRefs);
                                }
                            }
                        }
                    }
                    else if (propList.Key.FullName.Equals("System.Int64")) // Índice para tempo
                    {
                        foreach (var prop in propList.Value)
                        {
                            propInst = prop.GetValue(cacheItem, null);

                            if (propInst != null)
                            {
                                DateTime dateInst = ((DateTime)propInst).Date;
                                long timeValue = dateInst.Ticks;
                                IList<long> cacheItemRefs = new List<long>();

                                if (!timeCacheIndex.ContainsKey(className))
                                    timeCacheIndex.Add(className, new SortedDictionary<long, IList<long>>());

                                if (timeCacheIndex[className].ContainsKey(timeValue))
                                {
                                    if (!timeCacheIndex[className][timeValue].Contains(itemId))
                                        timeCacheIndex[className][timeValue].Add(itemId);
                                }
                                else
                                {
                                    cacheItemRefs.Add(itemId);
                                    timeCacheIndex[className].Add(timeValue, cacheItemRefs);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var prop in propList.Value) // Índice para texto (palavras)
                        {
                            propInst = prop.GetValue(cacheItem, null);

                            if (propInst != null)
                            {
                                string textValue = propInst.ToString().Trim();
                                textValue = textValue.Replace("(", string.Empty)
                                                     .Replace(")", string.Empty)
                                                     .Replace("-", string.Empty)
                                                     .Replace(".", string.Empty)
                                                     .Replace(",", string.Empty)
                                                     .Replace(":", string.Empty)
                                                     .Replace(@"""", string.Empty)
                                                     .Replace("\\", string.Empty)
                                                     .Replace("/", string.Empty)
                                                     .Replace("\n", " ")
                                                     .Replace("\r", " ");

                                if (!textCacheIndex.ContainsKey(className))
                                    textCacheIndex.Add(className, new SortedDictionary<string, IList<long>>());

                                string[] textWords = getTextWords(textValue);
                                foreach (string word in textWords)
                                {
                                    double fakeDoub;
                                    bool numericValue = double.TryParse(word, out fakeDoub);
                                    IList<long> cacheItemRefs = new List<long>();

                                    if (!numericValue)
                                    {
                                        if (textCacheIndex[className].ContainsKey(word))
                                        {
                                            if (!textCacheIndex[className][word].Contains(itemId))
                                                textCacheIndex[className][word].Add(itemId);
                                        }
                                        else
                                        {
                                            cacheItemRefs.Add(itemId);
                                            textCacheIndex[className].Add(word, cacheItemRefs);
                                        }
                                    }
                                    else
                                    {
                                        if (!numericCacheIndex.ContainsKey(className))
                                            numericCacheIndex.Add(className, new SortedDictionary<double, IList<long>>());

                                        if (numericCacheIndex[className].ContainsKey(fakeDoub))
                                        {
                                            if (!numericCacheIndex[className][fakeDoub].Contains(itemId))
                                                numericCacheIndex[className][fakeDoub].Add(itemId);
                                        }
                                        else
                                        {
                                            cacheItemRefs.Add(itemId);
                                            numericCacheIndex[className].Add(fakeDoub, cacheItemRefs);
                                        }
                                    }
                                }
                            }
                        }
                    }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static string mountSelectionQuery(string selection)
        {
            string result = "new (";

            if (selection.Contains('|'))
                result = string.Concat(result, selection.Replace("|", ", "), ")");
            else
                result = string.Concat(result, selection, ")");

            return result;
        }

        // Quebra um texto em palavras para indexação
        private static string[] getTextWords(string source)
        {
            return source.ToLower().Split(' ').Where(src => src.Length > 2).ToArray();
        }

        private static IDictionary<long, bool> deepIndex(string filter, IDictionary<long, bool> existentKeys, string className = "")
        {
            IDictionary<long, bool> foundedKeys = new ConcurrentDictionary<long, bool>();

            if (!string.IsNullOrEmpty(className))
            {
                if (textCacheIndex[className].ContainsKey(filter))
                    foreach (long index in textCacheIndex[className][filter])
                        if (!foundedKeys.ContainsKey(index))
                            foundedKeys.Add(index, false);
            }
            else
            {
                foreach (var textCache in textCacheIndex)
                {
                    if (textCache.Value.ContainsKey(filter))
                        foreach (long index in textCache.Value[filter])
                            if (!foundedKeys.ContainsKey(index))
                                foundedKeys.Add(index, false);
                }
            }

            if (existentKeys.Any())
                return foundedKeys.Where(fkey => existentKeys.ContainsKey(fkey.Key))
                                  .ToDictionary(fkey => fkey.Key, fkey => fkey.Value);
            else
                return foundedKeys;
        }

        private static IEnumerable<object> deepGet(string filter, string className = "")
        {
            IDictionary<long, bool> foundedKeys = new ConcurrentDictionary<long, bool>();

            string[] filterWords = filter.Split('|');

            foreach (string word in filterWords)
                foundedKeys = deepIndex(word, foundedKeys, className);

            if (string.IsNullOrEmpty(className))
            {
                List<object> result = new List<object>();

                foreach (var cacheItem in cacheItems.Values)
                    result.AddRange(cacheItem.Where(cci => foundedKeys.ContainsKey(cci.Key)).Select(cci => cci.Value));

                return result;
            }
            else
                return cacheItems[className].Where(cci => foundedKeys.ContainsKey(cci.Key)).Select(cci => cci.Value);
        }

        private static IEnumerable<object> getAll(string className = "")
        {
            IEnumerable<object> result = null;
            List<object> multiResult = new List<object>();

            if (!string.IsNullOrEmpty(className))
            {
                if (cacheItems[className] != null)
                    result = cacheItems[className].Select(cci => cci.Value).ToList();
            }
            else
            {
                foreach (var cacheItem in cacheItems)
                    multiResult.AddRange(cacheItem.Value.Select(cci => cci.Value));

                result = multiResult;
            }

            return result;
        }

        private static IEnumerable<object> getNumIndexValues(double fakeDoub, IEnumerable<long> foundedKeys, List<long> foundedMultiKeys, string className)
        {
            IEnumerable<object> result = null;
            List<object> multiResult = null;

            if (string.IsNullOrEmpty(className))
            {
                multiResult = new List<object>();

                foreach (var numCache in numericCacheIndex)
                    if (numCache.Value.ContainsKey(fakeDoub))
                        foundedMultiKeys.AddRange(numCache.Value[fakeDoub]);

                foreach (var cacheItem in cacheItems)
                    multiResult.AddRange(cacheItem.Value.Where(cci => foundedMultiKeys.Contains(cci.Key)).Select(cci => cci.Value));

                result = multiResult;
            }
            else
            {
                if (numericCacheIndex[className].ContainsKey(fakeDoub))
                    foundedKeys = numericCacheIndex[className][fakeDoub];

                result = cacheItems[className].Where(cci => foundedKeys.Contains(cci.Key)).Select(cci => cci.Value);
            }

            return result;
        }

        private static IEnumerable<object> getTimeIndexValues(DateTime fakeDate, IEnumerable<long> foundedKeys, List<long> foundedMultiKeys, string className)
        {
            IEnumerable<object> result = null;
            List<object> multiResult = null;

            long timeTicks = fakeDate.Ticks;

            if (string.IsNullOrEmpty(className))
            {
                multiResult = new List<object>();

                foreach (var timeCache in timeCacheIndex)
                    if (timeCache.Value.ContainsKey(timeTicks))
                        foundedMultiKeys.AddRange(timeCache.Value[timeTicks]);

                foreach (var cacheItem in cacheItems)
                    multiResult.AddRange(cacheItem.Value.Where(cci => foundedMultiKeys.Contains(cci.Key)).Select(cci => cci.Value));

                result = multiResult;
            }
            else
            {
                if (timeCacheIndex[className].ContainsKey(timeTicks))
                    foundedKeys = timeCacheIndex[className][timeTicks];

                result = cacheItems[className].Where(cci => foundedKeys.Contains(cci.Key)).Select(cci => cci.Value);
            }

            return result;
        }

        #endregion
    }
}
