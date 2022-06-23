using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Intersoft.CISSA.BizService.Utils;
using Intersoft.CISSA.DataAccessLayer.Core;
using Intersoft.CISSA.DataAccessLayer.Model.Context;
using Intersoft.CISSA.DataAccessLayer.Model.Query.Builders;
using Intersoft.CISSA.DataAccessLayer.Model.Workflow;
using Intersoft.CISSA.DataAccessLayer.Model.Query.Sql;
using Intersoft.CISSA.DataAccessLayer.Model.Data;
using Intersoft.CISSA.DataAccessLayer.Model.Documents;
using Intersoft.CISSA.DataAccessLayer.Repository;

using CISSA_REST_API.Models;
using Intersoft.CISSA.DataAccessLayer.Model.Enums;
using Intersoft.CISSA.DataAccessLayer.Model.Query;
using CISSA_REST_API.Models.Address;

namespace CISSA_REST_API.Util
{
    public static class ScriptExecutor
    {
        static IAppServiceProvider InitProvider(string username, Guid userId)
        {
            var dataContextFactory = DataContextFactoryProvider.GetFactory();

            var dataContext = dataContextFactory.CreateMultiDc("DataContexts");
            BaseServiceFactory.CreateBaseServiceFactories();
            var providerFactory = AppServiceProviderFactoryProvider.GetFactory();
            var provider = providerFactory.Create(dataContext);
            var serviceRegistrator = provider.Get<IAppServiceProviderRegistrator>();
            serviceRegistrator.AddService(new UserDataProvider(userId, username));
            return provider;
        }

        internal static object AdoptedChildren(YearMonthRequest request)
        {
            throw new NotImplementedException();
        }

        public static WorkflowContext CreateContext(string username, Guid? userId)
        {
            if(userId == null)
            {
                userId = new Guid("{DCED7BEA-8A93-4BAF-964B-232E75A758C5}");
            }
            return new WorkflowContext(new WorkflowContextData(Guid.Empty, userId.Value), InitProvider(username, userId.Value));
        }

        public static IEnumerable<attribute> GetDocAttributesByDefId(Guid defId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docDefRepo = context.DocDefs;
            var docDef = docDefRepo.Find(defId);
            if (docDef != null)
                return docDef.Attributes.Select(x => new attribute
                {
                    id = x.Id,
                    name = x.Name,
                    caption = x.Caption,
                    type = x.Type.Name,
                    enumDef = x.EnumDefType != null ? x.EnumDefType.Id : Guid.Empty
                });
            throw new ApplicationException("Not found DefId: " + defId);
        }

        public static IEnumerable<document> GetDocumentsByDefId(Guid docDefId, int page, int size, Guid userId, Guid? docId = null)
        {
            var userObj = DAL.GetCissaUser(userId);
            if (userObj == null) throw new ApplicationException("Пользователь не найден!");
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docDefRepo = context.DocDefs;
            var docDef = docDefRepo.Find(docDefId);

            var qb = new QueryBuilder(docDefId);

            if(docId != null)
            {
                qb.Where("&Id").Eq(docId);
            }

            var query = SqlQueryBuilder.Build(context.DataContext, qb.Def);

            query.AddAttribute("&Id");
            foreach (var attr in docDef.Attributes)
            {
                System.Diagnostics.Trace.WriteLine(attr.Name+ "\t"+attr.Caption + "\t" + attr.Type.Name);
                query.AddAttribute(attr.Name);
            }

            query.TopNo = size;
            query.SkipNo = (page - 1) * size;

            var documents = new List<document>();

            using (var reader = new SqlQueryReader(context.DataContext, query))
            {
                while (reader.Read())
                {
                    var doc = new document() {
                        id = reader.GetGuid(0)
                    };

                    var attributes = new List<attribute>();
                    for (int i = 1; i < reader.Fields.Count; i++)
                    {
                        var docAttr = new attribute()
                        {
                            id = reader.Fields[i].AttributeId,
                            name = reader.Fields[i].AttributeName,
                            type = reader.Fields[i].AttrDef.Type.Name,
                            caption = reader.Fields[i].AttrDef.Caption
                        };

                        if (docAttr.type == "Enum")
                        {
                            docAttr.enumDef = reader.Fields[i].AttrDef.EnumDefType.Id;
                        }

                        if (!reader.IsDbNull(i))
                        {
                            System.Diagnostics.Trace.Write(reader.GetValue(i) + "\t");
                            docAttr.value = reader.GetValue(i).ToString();

                            if (docAttr.type == "Enum")
                            {
                                var enumValueId = reader.GetGuid(i);
                                docAttr.enumValueText = context.Enums.GetValue(enumValueId).Value;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Trace.Write("null\t");
                            docAttr.value = "";
                        }

                        attributes.Add(docAttr);

                    }

                    System.Diagnostics.Trace.WriteLine("");
                    doc.attributes = attributes.ToArray();
                    documents.Add(doc);
                    
                }
            }

            return documents;


        }

        internal static Guid GetDefId(Guid id, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            return context.Documents.LoadById(id).DocDef.Id;
        }

        public static void SetState(Guid docId, Guid stateTypeId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            context.Documents.SetDocState(docId, stateTypeId);
        }

        public static int CountDocumentsByDefId(Guid docDefId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docDefRepo = context.DocDefs;
            var docDef = docDefRepo.Find(docDefId);

            var qb = new QueryBuilder(docDefId);

            var query = SqlQueryBuilder.Build(context.DataContext, qb.Def);

            query.AddAttribute("&Id", SqlQuerySummaryFunction.Count);
            using (var reader = new SqlQueryReader(context.DataContext, query))
            {
                if (reader.Read())
                {
                    return reader.GetInt32(0);
                }
            }

            return 0;
        }
        static void InitQueryConditions(WorkflowContext context, SqlQuery query, document filterDocument, Guid defId, SqlQuerySource querySource)
        {
            if (filterDocument != null && filterDocument.attributes != null && filterDocument.attributes.Length > 0)
            {
                filterDocument.attributes = filterDocument.attributes.Where(x => !string.IsNullOrEmpty(x.value) || x.type == "Doc").ToArray();
                var cissaDocument = CreateDocumentInstance(context, defId, filterDocument);
                foreach (var attr in filterDocument.attributes)
                {
                    if (attr.type != "Doc" && cissaDocument[attr.name] != null)
                        query.AddCondition(ExpressionOperation.And, defId, attr.name, ConditionOperation.Equal, cissaDocument[attr.name]);
                    if (attr.type == "Doc" && cissaDocument[attr.name] != null)
                        query.AddCondition(ExpressionOperation.And, defId, attr.name, ConditionOperation.Equal, cissaDocument[attr.name]);
                    if (attr.type == "Doc" && attr.subDocument != null && attr.subDocument.attributes != null && attr.subDocument.attributes.Length > 0)
                    {
                        var newQuerySource = query.JoinSource(querySource, attr.docDef, SqlSourceJoinType.Inner, attr.name);
                        InitQueryConditions(context, query, attr.subDocument, attr.docDef, newQuerySource);
                    }
                }
            }
        }
        public static IEnumerable<document> FilterDocumentsByDefId(document filterDocument, Guid docDefId, int page, int size, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docDefRepo = context.DocDefs;
            //var docDef = docDefRepo.Find(docDefId);

            var qb = new QueryBuilder(docDefId);
            var query = SqlQueryBuilder.Build(context.DataContext, qb.Def);

            InitQueryConditions(context, query, filterDocument, docDefId, query.Source);

            query.AddAttribute("&Id");
            
            query.TopNo = size;
            query.SkipNo = (page - 1) * size;

            var documents = new List<document>();
            var table = new System.Data.DataTable();
            using (var reader = new SqlQueryReader(context.DataContext, query))
            {
                reader.Open();
                reader.Fill(table);
                reader.Close();
            }
            foreach(System.Data.DataRow row in table.Rows)
            {
                var id = (Guid)row[0];
                var doc = new document
                {
                    id = id
                };
                var d = context.Documents.LoadById(id);
                var attributes = new List<attribute>();
                foreach (var attr in d.Attributes)
                {
                    var docAttr = new attribute()
                    {
                        id = attr.AttrDef.Id,
                        name = attr.AttrDef.Name,
                        type = attr.AttrDef.Type.Name,
                        caption = attr.AttrDef.Caption
                    };
                    if (docAttr.type == "Enum")
                    {
                        docAttr.enumDef = attr.AttrDef.EnumDefType.Id;
                    }
                    if (attr.ObjectValue != null)
                    {
                        docAttr.value = attr.ObjectValue.ToString();
                        if (docAttr.type == "Enum")
                        {
                            var enumValueId = (Guid)attr.ObjectValue;
                            docAttr.enumValueText = context.Enums.GetValue(enumValueId).Value;
                        }
                        if (docAttr.type == "Doc")
                        {
                            var docValueId = (Guid)attr.ObjectValue;
                            docAttr.subDocument = GetDocumentById(docValueId, userId);//context.Enums.GetValue(enumValueId).Value;
                        }
                    }
                    attributes.Add(docAttr);
                }
                var state = context.Documents.GetDocState(id);
                attributes.Add(new attribute
                {
                    type = "State",
                    name = "State",
                    caption = "Статус",
                    value = state != null ? state.Type.Name : "-"
                });
                doc.attributes = attributes.ToArray();
                documents.Add(doc);
            }

            return documents;


        }

        public static IEnumerable<document> FilterDocumentsByDefIdState(document filterDocument, Guid docDefId, int page, int size, Guid userId, Guid stateTypeId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docDefRepo = context.DocDefs;
            var docDef = docDefRepo.Find(docDefId);

            var qb = new QueryBuilder(docDefId);
            var query = SqlQueryBuilder.Build(context.DataContext, qb.Def);

            InitQueryConditions(context, query, filterDocument, docDefId, query.Source);

            if (stateTypeId != Guid.Empty)
            {
                query.AddCondition(ExpressionOperation.And, docDef, "&State", ConditionOperation.Equal, stateTypeId);
            }

            query.AddAttribute("&Id");

            query.TopNo = size;
            query.SkipNo = (page - 1) * size;

            var documents = new List<document>();
            var table = new System.Data.DataTable();
            using (var reader = new SqlQueryReader(context.DataContext, query))
            {
                reader.Open();
                reader.Fill(table);
                reader.Close();
            }
            foreach (System.Data.DataRow row in table.Rows)
            {
                var id = (Guid)row[0];
                var doc = new document
                {
                    id = id
                };
                var d = context.Documents.LoadById(id);
                var attributes = new List<attribute>();
                foreach (var attr in d.Attributes)
                {
                    var docAttr = new attribute()
                    {
                        id = attr.AttrDef.Id,
                        name = attr.AttrDef.Name,
                        type = attr.AttrDef.Type.Name,
                        caption = attr.AttrDef.Caption
                    };
                    if (docAttr.type == "Enum")
                    {
                        docAttr.enumDef = attr.AttrDef.EnumDefType.Id;
                    }
                    if (attr.ObjectValue != null)
                    {
                        docAttr.value = attr.ObjectValue.ToString();
                        if (docAttr.type == "Enum")
                        {
                            var enumValueId = (Guid)attr.ObjectValue;
                            docAttr.enumValueText = context.Enums.GetValue(enumValueId).Value;
                        }
                        if (docAttr.type == "Doc")
                        {
                            var docValueId = (Guid)attr.ObjectValue;
                            docAttr.subDocument = GetDocumentById(docValueId, userId);//context.Enums.GetValue(enumValueId).Value;
                        }
                    }
                    attributes.Add(docAttr);
                }
                var state = context.Documents.GetDocState(id);
                attributes.Add(new attribute
                {
                    type = "State",
                    name = "State",
                    caption = "Статус",
                    value = state != null ? state.Type.Name : "-"
                });
                doc.attributes = attributes.ToArray();
                documents.Add(doc);
            }

            return documents;


        }
        public static int CountFilteredDocumentsByDefId(document filterDocument, Guid docDefId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docDefRepo = context.DocDefs;
            var docDef = docDefRepo.Find(docDefId);

            var qb = new QueryBuilder(docDefId);

            var query = SqlQueryBuilder.Build(context.DataContext, qb.Def);

            if (filterDocument != null && filterDocument.attributes != null && filterDocument.attributes.Length > 0)
            {
                filterDocument.attributes = filterDocument.attributes.Where(x => !string.IsNullOrEmpty(x.value)).ToArray();
                var cissaDocument = CreateDocumentInstance(context, docDefId, filterDocument);
                foreach (var attr in filterDocument.attributes)
                {
                    query.AddCondition(ExpressionOperation.And, docDef, attr.name, ConditionOperation.Equal, cissaDocument[attr.name]);
                }
            }

            query.AddAttribute("&Id", SqlQuerySummaryFunction.Count);
            using (var reader = new SqlQueryReader(context.DataContext, query))
            {
                if (reader.Read())
                {
                    return reader.GetInt32(0);
                }
            }

            return 0;
        }

        private static bool IsHidden(Guid docDefId, Guid docId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docDefRepo = context.DocDefs;
            var docDef = docDefRepo.Find(docDefId);

            var qb = new QueryBuilder(docDefId);

            if (docId != null)
            {
                qb.Where("&Id").Eq(docId);
            }

            var query = SqlQueryBuilder.Build(context.DataContext, qb.Def);

            query.AddAttribute("&Id", SqlQuerySummaryFunction.Count);
            using (var reader = new SqlQueryReader(context.DataContext, query))
            {
                if (reader.Read())
                {
                    return reader.GetInt32(0) == 0;
                }
            }
            return true;
        }

        public static document GetDocumentById(Guid docId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docRepo = context.Documents;
            var docDefRepo = context.DocDefs;

            var d = docRepo.LoadById(docId);

            if(IsHidden(d.DocDef.Id, docId, userId))
            {
                throw new ApplicationException("Данный документ был удален пользователем!");
            }

            var doc = new document()
            {
                id = docId
            };

            
            var attributes = new List<attribute>();
            foreach (var attr in d.Attributes)
            {
                var docAttr = new attribute()
                {
                    id = attr.AttrDef.Id,
                    name = attr.AttrDef.Name,
                    type = attr.AttrDef.Type.Name,
                    caption = attr.AttrDef.Caption
                };
                if (docAttr.type == "Enum")
                {
                    if (attr.AttrDef.EnumDefType == null) throw new ApplicationException("Справочник для поля не указан: " + attr.AttrDef.Name);
                    docAttr.enumDef = attr.AttrDef.EnumDefType.Id;
                }
                if (docAttr.type == "Doc")
                {
                    if (attr.AttrDef.DocDefType == null) throw new ApplicationException("DefId документа для поля не указан: " + attr.AttrDef.Name);
                    docAttr.docDef = attr.AttrDef.DocDefType.Id;
                }
                if (attr.ObjectValue != null)
                {
                    docAttr.value = attr.ObjectValue.ToString();
                    if (docAttr.type == "Enum")
                    {
                        var enumValueId = (Guid)attr.ObjectValue;
                        docAttr.enumValueText = context.Enums.GetValue(enumValueId).Value;
                    }

                    if (docAttr.type == "Doc")
                    {
                        var docValueId = (Guid)attr.ObjectValue;
                        docAttr.subDocument = GetDocumentById(docValueId, userId);//context.Documents.LoadById(docValueId);
                    }
                }
                attributes.Add(docAttr);
            }
            var state = context.Documents.GetDocState(docId);
            attributes.Add(new attribute
            {
                type = "State",
                name = "State",
                caption = "Статус",
                value = state != null ? state.Type.Name : "-"
            });
            doc.attributes = attributes.ToArray();
            return doc;
        }

        public static List<DAL.MenuItem> GetMenuItems()
        {
            return DAL.GetMenuItems();
        }

        public static IList<EnumValue> GetEnumItems(Guid enumDefId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            return context.Enums.GetEnumItems(enumDefId);
        }

        public static Doc CreateDocumentInstance(WorkflowContext context, Guid defId, document document)
        {
            var docRepo = context.Documents;

            var doc = docRepo.New(defId);
            foreach (var attr in document.attributes)
            {
                object attrValue = null;
                if (new[] { "Enum", "Doc" }.Contains(attr.type))
                {
                    if (!string.IsNullOrEmpty(attr.value) && Guid.TryParse(attr.value, out Guid parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Text")
                {
                    attrValue = attr.value;
                }
                else if (attr.type == "DateTime")
                {
                    if (!string.IsNullOrEmpty(attr.value) && DateTime.TryParse(attr.value, out DateTime parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Int")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Int32.TryParse(attr.value, out Int32 parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Currency")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Decimal.TryParse(attr.value, out Decimal parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Bool")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Boolean.TryParse(attr.value, out Boolean parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Float")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Double.TryParse(attr.value, out Double parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else
                    throw new ApplicationException("Не могу сохранить документ, тип атрибута не распознан. attr.Type: " + attr.type);
                if (attrValue != null)
                    doc[attr.name] = attrValue;
            }
            return doc;
        }

        public static Guid CreateDocument(Guid defId, document document, Guid userId, bool withNo = false, string noAttrName = "")
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docRepo = context.Documents;

            var doc = CreateDocumentInstance(context, defId, document);
            if (withNo)
            {
                var no = GeneratorRepository.GetNewId(doc.OrganizationId.Value, defId).ToString();
                while (no.Length < 9) no = "0" + no;
                doc[noAttrName] = "N" + no;
            }
            docRepo.Save(doc);
            return doc.Id;
        }
        public static void UpdateDocument(Guid docId, document document, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);

            var docRepo = context.Documents;

            var doc = docRepo.LoadById(docId);
            foreach (var attr in document.attributes)
            {
                object attrValue = null;

                if (new[] { "Enum", "Doc" }.Contains(attr.type))
                {
                    if (!string.IsNullOrEmpty(attr.value) && Guid.TryParse(attr.value, out Guid parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Text")
                {
                    attrValue = attr.value;
                }
                else if (attr.type == "DateTime")
                {
                    if (!string.IsNullOrEmpty(attr.value) && DateTime.TryParse(attr.value, out DateTime parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Int")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Int32.TryParse(attr.value, out Int32 parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Currency")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Decimal.TryParse(attr.value, out Decimal parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Bool")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Boolean.TryParse(attr.value, out Boolean parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else if (attr.type == "Float")
                {
                    if (!string.IsNullOrEmpty(attr.value) && Double.TryParse(attr.value, out Double parsedValue))
                    {
                        attrValue = parsedValue;
                    }
                }
                else
                    throw new ApplicationException("Не могу сохранить документ, тип атрибута не распознан. attr.Type: " + attr.type);
                //if (attrValue != null)
                    doc[attr.name] = attrValue;
            }
            docRepo.Save(doc);
        }

        public static void DeleteDocument(Guid docId, Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            context.Documents.HideById(docId);
        }

        #region Address

        static Guid regionDefId = new Guid("{99D6F2C3-C138-4BDD-BD5B-BCAE0EF11AC6}");
        static Guid districtDefId = new Guid("{A3FCA356-82A9-4BBD-872A-8333BEC6E41A}");
        static Guid cityDefId = new Guid("{4BB6D32D-5181-4031-BA49-CF5910D6D883}");
        static Guid settlementDefId = new Guid("{80CC229E-05FC-45A5-B2ED-F101465ADD1E}");
        static Guid villageDefId = new Guid("{BD0E1850-FFE9-4EBA-B86E-98069DF7B885}");
        public static IEnumerable<region> GetRegions(Guid userId)
        {
            var userObj = DAL.GetCissaUser(userId);
            if (userObj == null) throw new ApplicationException("Пользователь не найден!");
            var context = CreateContext(userObj.UserName, userObj.Id);
            var qb = new QueryBuilder(regionDefId);
            var query = context.CreateSqlQuery(qb.Def);
            query.AddAttributes(new[] { "&Id", "Name" });
            using (var reader = context.CreateSqlReader(query))
            {
                while (reader.Read())
                    yield return new region { id = reader.GetGuid(0), name = reader.IsDbNull(1) ? "" : reader.GetString(1) };
            }
        }

        public static IEnumerable<district> GetDistricts(Guid userId, Guid? regionId = null)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            var qb = new QueryBuilder(districtDefId);
            if (regionId != null)
                qb.Where("Area").Eq(regionId);
            var query = context.CreateSqlQuery(qb.Def);
            query.AddAttributes(new[] { "&Id", "Name", "DistrictType" });
            if (regionId == null)
                query.AddAttribute("Area");
            using (var reader = context.CreateSqlReader(query))
            {
                while (reader.Read())
                    yield return new district
                    {
                        id = reader.GetGuid(0),
                        name = reader.IsDbNull(1) ? "" : reader.GetString(1),
                        districtType = reader.IsDbNull(2) ? null : (Guid?)reader.GetGuid(2),
                        regionId = regionId.HasValue ? regionId : reader.GetGuid(3)
                    };
            }
        }

        public static IEnumerable<city> GetCities(Guid userId, Guid? districtId = null)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            var qb = new QueryBuilder(cityDefId);
            if (districtId != null)
                qb.Where("District").Eq(districtId);
            var query = context.CreateSqlQuery(qb.Def);
            query.AddAttributes(new[] { "&Id", "Name", "DistrictType" });
            if (districtId == null)
                query.AddAttribute("District");
            using (var reader = context.CreateSqlReader(query))
            {
                while (reader.Read())
                    yield return new city
                    {
                        id = reader.GetGuid(0),
                        name = reader.IsDbNull(1) ? "" : reader.GetString(1),
                        districtType = reader.IsDbNull(2) ? null : (Guid?)reader.GetGuid(2),
                        districtId = districtId.HasValue ? districtId : reader.GetGuid(3)
                    };
            }
        }

        public static IEnumerable<settlement> GetSettlements(Guid userId, Guid? districtId = null)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            var qb = new QueryBuilder(settlementDefId);
            if (districtId != null)
                qb.Where("District").Eq(districtId);
            var query = context.CreateSqlQuery(qb.Def);
            query.AddAttributes(new[] { "&Id", "Name", "DistrictType" });
            if (districtId == null)
                query.AddAttribute("District");
            using (var reader = context.CreateSqlReader(query))
            {
                while (reader.Read())
                    yield return new settlement
                    {
                        id = reader.GetGuid(0),
                        name = reader.IsDbNull(1) ? "" : reader.GetString(1),
                        districtType = reader.IsDbNull(2) ? null : (Guid?)reader.GetGuid(2),
                        districtId = districtId.HasValue ? districtId : reader.GetGuid(3)
                    };
            }
        }

        public static IEnumerable<village> GetVillages(Guid userId, Guid? settlementId = null)
        {
            var userObj = DAL.GetCissaUser(userId);
            var context = CreateContext(userObj.UserName, userObj.Id);
            var qb = new QueryBuilder(villageDefId);
            if (settlementId != null)
                qb.Where("Settlement").Eq(settlementId);
            var query = context.CreateSqlQuery(qb.Def);
            query.AddAttributes(new[] { "&Id", "Name", "Coefficient" });
            if (settlementId == null)
                query.AddAttribute("Settlement");
            using (var reader = context.CreateSqlReader(query))
            {
                while (reader.Read())
                    yield return new village
                    {
                        id = reader.GetGuid(0),
                        name = reader.IsDbNull(1) ? "" : reader.GetString(1),
                        coefficient = reader.IsDbNull(2) ? null : (double?)reader.GetDouble(2),
                        settlementId = settlementId.HasValue ? settlementId : reader.GetGuid(3)
                    };
            }
        }

        #endregion

        public static MSECDetails GetMSECDetails(PINRequest request)
        {
            var context = CreateContext("msec_system_user", new Guid("{4C55D519-8576-4EED-82B5-A1F120BFA1CB}"));

            var grownMsecDefId = new Guid("{12AE380B-39CE-4753-A556-4083BD4B0F92}");
            var childMsecDefId = new Guid("{0ECF44B6-2B2D-4687-B7B0-07BA5F942AC6}");
            var personDefId = new Guid("{D71CE61A-9B59-4B5E-8713-8131DBB5BA02}");
            var approvedStateTypeId = new Guid("{32062CB7-C31C-4AFB-ADF3-F9F9AEEFCE59}");

            var qb = new QueryBuilder(grownMsecDefId, context.UserId);
            qb.Where("Person").Include("PIN").Eq(request.PIN).End().And("&State").Eq(approvedStateTypeId);
            var query = context.CreateSqlQuery(qb.Def);
            query.AddAttribute(query.Source, "&OrgId");
            query.AddAttribute(query.Source, "DateOfExamenation");
            query.AddAttribute(query.Source, "DisabilityGroup");
            query.AddAttribute(query.Source, "Examination1");
            query.AddAttribute(query.Source, "FromDisabilityFixedPeriod");
            query.AddAttribute(query.Source, "DisabilityFixedPeriod");
            query.AddAttribute(query.Source, "Indefinitely");
            query.AddAttribute(query.Source, "TimeOfDisability");
            query.AddAttribute(query.Source, "DateOfReexamenation");
            query.AddAttribute(query.Source, "IsNotSubject");
            query.AddAttribute(query.Source, "year");
            using (var reader = context.CreateSqlReader(query))
            {
                if (reader.Read())
                {
                    var modelObj = new MSECDetails
                    {
                        StatusCode = "SUCCESS",
                        OrganizationName = reader.IsDbNull(0) ? "" : context.Orgs.GetOrgName(reader.GetGuid(0)),
                        ExaminationDate = reader.IsDbNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                        DisabilityGroup = reader.IsDbNull(2) ? "" : context.Enums.GetValue(reader.GetGuid(2)).Value,
                        ExaminationType = reader.IsDbNull(3) ? "" : context.Enums.GetValue(reader.GetGuid(3)).Value,
                        From = reader.IsDbNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                        To = reader.IsDbNull(5) ? (reader.IsDbNull(6) || !reader.GetBoolean(6) ? DateTime.MinValue : DateTime.MaxValue) : reader.GetDateTime(5),
                        TimeOfDisability = reader.IsDbNull(7) ? DateTime.MinValue : reader.GetDateTime(7)
                    };
                    var dateOfReexamenation = reader.IsDbNull(8) ? Guid.Empty : reader.GetGuid(8);
                    var isNotSubject = reader.IsDbNull(9) ? false : reader.GetBoolean(9);
                    var year = reader.IsDbNull(10) ? "" : reader.GetString(10);

                    if (isNotSubject)
                    {
                        modelObj.ReExamination = "не подлежит";
                    }
                    else
                    {
                        modelObj.ReExamination = string.Format("{0} {1}", (dateOfReexamenation != Guid.Empty ? context.Enums.GetValue(dateOfReexamenation).Value : ""), year);
                    }

                    return modelObj;
                }
            }

            qb = new QueryBuilder(childMsecDefId, context.UserId);
            qb.Where("Person").Include("PIN").Eq(request.PIN).End().And("&State").Eq(approvedStateTypeId);
            query = context.CreateSqlQuery(qb.Def);
            query.AddAttribute(query.Source, "&OrgId");
            query.AddAttribute(query.Source, "DateOfExamenation");
            query.AddAttribute(query.Source, "goal");
            query.AddAttribute(query.Source, "Examination1");
            query.AddAttribute(query.Source, "DisabilityFixedPeriod");
            query.AddAttribute(query.Source, "DisabilitySetForPeriodOf");
            query.AddAttribute(query.Source, "TimeOfDisability");
            query.AddAttribute(query.Source, "DateOfReexamenation");
            query.AddAttribute(query.Source, "year");
            using (var reader = context.CreateSqlReader(query))
            {
                if (reader.Read())
                {
                    var modelObj = new MSECDetails
                    {
                        StatusCode = "SUCCESS",
                        OrganizationName = reader.IsDbNull(0) ? "" : context.Orgs.GetOrgName(reader.GetGuid(0)),
                        ExaminationDate = reader.IsDbNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                        DisabilityGroup = reader.IsDbNull(2) || !reader.GetBoolean(2) ? "Инвалидность не установлена" : "Ребенок с ОВЗ",//reader.IsDbNull(2) ? "" : context.Enums.GetValue(reader.GetGuid(2)).Value,
                        ExaminationType = reader.IsDbNull(3) ? "" : context.Enums.GetValue(reader.GetGuid(3)).Value,
                        From = reader.IsDbNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                        To = reader.IsDbNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                        TimeOfDisability = reader.IsDbNull(6) ? DateTime.MinValue : reader.GetDateTime(6)
                    };

                    var dateOfReexamenation = reader.IsDbNull(7) ? Guid.Empty : reader.GetGuid(7);
                    var year = reader.IsDbNull(8) ? "" : reader.GetString(8);

                    modelObj.ReExamination = string.Format("{0} {1}", (dateOfReexamenation != Guid.Empty ? context.Enums.GetValue(dateOfReexamenation).Value : ""), year);

                    return modelObj;
                }
            }

            return new MSECDetails { StatusCode = "NOT_FOUND", DisabilityGroup = "", ExaminationType = "", OrganizationName = "", ReExamination = "" };
        }
    }

}
