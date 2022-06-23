using CISSA_REST_API.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Document = CISSA_REST_API.Models.document;
using Attribute = CISSA_REST_API.Models.attribute;
using System.IO;

namespace CISSA_REST_API.Controllers
{
    public class DocumentController : ApiController
    {
        [HttpGet]
        [ResponseType(typeof(Document[]))]
        public IHttpActionResult GetDocumentsByDefId([FromUri] Guid defId, [FromUri] int size, [FromUri] int page, [FromUri]Guid userId)
        {
            try
            {
                var result = ScriptExecutor.GetDocumentsByDefId(defId, page, size, userId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(string.Format("Error text: {0}, stacktrace: {1}", e.Message, e.StackTrace));
            }
        }

        [HttpPost]
        [ResponseType(typeof(Document[]))]
        public IHttpActionResult FilterDocumentsByDefId([FromUri] Guid defId, [FromUri] int size, [FromUri] int page, [FromUri]Guid userId, [FromBody] Document filterDocument)
        {
            try
            {
                //WriteLog(Newtonsoft.Json.JsonConvert.SerializeObject(filterDocument));
                var result = ScriptExecutor.FilterDocumentsByDefId(filterDocument, defId, page, size, userId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost]
        [ResponseType(typeof(Document[]))]
        public IHttpActionResult FilterDocumentsByDefIdState([FromUri] Guid defId, [FromUri] int size, [FromUri] int page, [FromUri]Guid userId, [FromUri]Guid stateTypeId, [FromBody] Document filterDocument)
        {
            try
            {
                //WriteLog(Newtonsoft.Json.JsonConvert.SerializeObject(filterDocument));
                var result = ScriptExecutor.FilterDocumentsByDefIdState(filterDocument, defId, page, size, userId, stateTypeId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost]
        [ResponseType(typeof(int))]
        public IHttpActionResult CountFilteredDocumentsByDefId([FromUri] Guid defId, [FromUri]Guid userId, [FromBody] Document filterDocument)
        {
            try
            {
                var result = ScriptExecutor.CountFilteredDocumentsByDefId(filterDocument, defId, userId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        [ResponseType(typeof(Attribute[]))]
        public IHttpActionResult GetDocAttributesByDefId([FromUri] Guid defId, [FromUri]Guid userId)
        {
            try
            {
                var result = ScriptExecutor.GetDocAttributesByDefId(defId, userId);
                return Ok(result);
            }
            catch (ApplicationException e)
            {
                return NotFound();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        [ResponseType(typeof(int))]
        public IHttpActionResult CountDocumentsByDefId([FromUri] Guid defId, [FromUri]Guid userId)
        {
            try
            {
                var result = ScriptExecutor.CountDocumentsByDefId(defId, userId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet]
        [ResponseType(typeof(Document))]
        public IHttpActionResult GetDocumentById([FromUri] Guid id, [FromUri]Guid userId)
        {
            try
            {
                var result = ScriptExecutor.GetDocumentById(id, userId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage Create([FromUri] Guid defId, [FromUri]Guid userId, [FromBody] Document document)
        {
            try
            {
                var docId = ScriptExecutor.CreateDocument(defId, document, userId);
                document.id = docId;
                var response = Request.CreateResponse(HttpStatusCode.Created, document);

                string uri = Url.Link("DefaultApi", new { action = "GetDocumentById", id = document.id });
                response.Headers.Location = new Uri(uri);
                return response;
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e);
            }
        }

        [HttpPost]
        public HttpResponseMessage CreateWithNo([FromUri] Guid defId, [FromUri]Guid userId, [FromUri]string noAttrName, [FromBody] Document document)
        {
            try
            {
                var docId = ScriptExecutor.CreateDocument(defId, document, userId, true, noAttrName);
                document.id = docId;
                var response = Request.CreateResponse(HttpStatusCode.Created, document);

                string uri = Url.Link("DefaultApi", new { action = "GetDocumentById", id = document.id });
                response.Headers.Location = new Uri(uri);
                return response;
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e);
            }
        }

        [HttpPut]
        public IHttpActionResult Update([FromUri] Guid id, [FromUri]Guid userId, [FromBody] Document document)
        {
            try
            {
                ScriptExecutor.UpdateDocument(id, document, userId);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.GetBaseException().Message);
            }
        }

        [HttpDelete]
        public IHttpActionResult Delete([FromUri] Guid id, [FromUri]Guid userId)
        {
            try
            {
                ScriptExecutor.DeleteDocument(id, userId);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.GetBaseException().Message);
            }
        }

        [HttpGet]
        public IHttpActionResult SetState([FromUri] Guid docId, [FromUri] Guid stateTypeId, [FromUri]Guid userId)
        {
            try
            {
                ScriptExecutor.SetState(docId, stateTypeId, userId);
                return Ok(new { result = true });
            }
            catch (Exception e)
            {
                return BadRequest(e.GetBaseException().Message);
            }
        }
        public class SetMultipleDocsToStateRequest
        {
            public Guid[] docIdList { get; set; }
            public Guid stateTypeId { get; set; }
        }
        [HttpPost]
        public IHttpActionResult SetMultipleDocsToState([FromBody] SetMultipleDocsToStateRequest docsToState, [FromUri]Guid userId)
        {
            try
            {
                foreach (var docId in docsToState.docIdList)
                {
                    ScriptExecutor.SetState(docId, docsToState.stateTypeId, userId);
                }
                return Ok(new { result = true });
            }
            catch (Exception e)
            {
                return BadRequest(e.GetBaseException().Message);
            }
        }

        [HttpGet]
        [ResponseType(typeof(Guid))]
        public IHttpActionResult GetDefId([FromUri] Guid Id, [FromUri]Guid userId)
        {
            try
            {
                Guid result = ScriptExecutor.GetDefId(Id, userId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.GetBaseException().Message);
            }
        }

        static void WriteLog(object text)
        {
            using (StreamWriter sw = new StreamWriter("c:\\Log\\cissa-rest-api.log", true))
            {
                sw.WriteLine(text.ToString());
            }
        }
    }
}
