using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.IO;

namespace Rochas.CacheServer
{
    public static class JsonFormatter
    {
        public static string JsonIdent(string srcJson, bool htmlOutput = false)
        {
            using (var stringReader = new StringReader(srcJson))
            using (var stringWriter = new StringWriter())
            using (var jsonReader = new JsonTextReader(stringReader))
            using (var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented })
            {
                jsonWriter.WriteToken(jsonReader);
                
                return stringWriter.ToString();
            }
        }
    }
}
