using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Web;
using System.Web.Http;

namespace Rochas.CacheServer.Controllers
{
    public class ConfigController : ApiController
    {
        public string Get(string filter, string modelName = "")
        {
            string result = string.Empty;

            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.Formatters.Add(new JsonMediaTypeFormatter());

            switch (filter)
            {
                case "reset":
                    CacheServer.Reset();
                    result = "Service cache was released.";
                    break;
                case "save":
                    CacheServer.Save(modelName);
                    result = "Service cache was saved.";
                    break;
                case "load":
                    CacheServer.Load(modelName);
                    result = "Service cache was reloaded.";
                    break;
                case "memory":
                    result = CacheServer.GetMemoryUsage();
                    break;
            }

            return result;
        }

        [HttpPost]
        public string Post()
        {
            MemoryStream asmBuffer = new MemoryStream();
            MemoryStream dataBuffer = new MemoryStream();
            StreamReader dataReader = null;
            string dataSource = string.Empty;
            string className = string.Empty;
            bool replaceInstance = false;

            // Obtem o buffer da biblioteca de modelos do cliente para realizar as consultas
            // Obtem o nome do modelo a utilizar e a fonte de dados composta JSON/XML

            if (HttpContext.Current.Request.Files.Count > 0)
            {
                HttpContext.Current.Request.Files[0].InputStream.CopyTo(asmBuffer);
                HttpContext.Current.Request.Files[1].InputStream.CopyTo(dataBuffer);

                dataBuffer.Position = 0;
                dataReader = new StreamReader(dataBuffer);
                dataSource = dataReader.ReadToEnd();

                className = HttpContext.Current.Request.Form["modelName"];
                replaceInstance = (HttpContext.Current.Request.Form["replaceInstance"] == "checked");
            }
            else if ((HttpContext.Current.Request.InputStream != null)
                  && (HttpContext.Current.Request.InputStream.Length > 0))
            {
                MemoryStream inputMemStream = new MemoryStream();
                List<byte[]> postParams = null;

                HttpContext.Current.Request.InputStream.CopyTo(inputMemStream);

                postParams = Serializer.DeserializeBinary(inputMemStream.ToArray()) as List<byte[]>;

                if (postParams != null)
                {
                    asmBuffer = new MemoryStream(postParams.First());
                    dataSource = Encoding.ASCII.GetString(postParams[1]);
                    className = Encoding.ASCII.GetString(postParams[2]);
                    replaceInstance = bool.Parse(Encoding.ASCII.GetString(postParams.Last()));
                }
                else
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
            }
            else
                throw new HttpResponseException(HttpStatusCode.NoContent);


            // Conteúdo recebido, realizando carga dos dados

            if (dataSource.StartsWith("<") && dataSource.EndsWith(">"))
                dataSource = CacheServer.ConvertXml(dataSource);

            if ((asmBuffer != null) && !string.IsNullOrEmpty(className))
            {
                CacheServer.Post(asmBuffer.ToArray(), dataSource, className, replaceInstance);

                return "OK.DataReceived";
            }
            else
                throw new HttpResponseException(HttpStatusCode.BadRequest);
        }
    }
}
