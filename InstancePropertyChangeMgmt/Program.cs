using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Transactions;
using System.Linq;
using System.Text;

namespace InstancePropertyChangeMgmt
{
    public class TransactionDetail
    {
        private int orgID = 0;
        private string messageToOrg = null;

        public TransactionDetail() { }

        public int OrgID
        {
            get { return orgID; }
            
        }
        public string MessageToOrg
        {
            get { return messageToOrg; }
            
        }
    }

    public class ObjectChangeHistoryManager
    {
        /// <summary>
        /// Data mapper class
        /// </summary>
        public class ObjectChangeHistoryManagerStorageHelper
        {
            public void saveChangeHistory(ObjectChangeHistoryManager outerObj)
            {
                int resultCount = 0;

                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required))
                {
                    //Use DBUtilities and query load balancing here to execute against Logs DB
                    SqlConnection con = new SqlConnection();//Would use DBUtilities.getConnection() here;
                    using (con)
                    {
                        
                        SqlCommand command = new SqlCommand();
                        command.Connection = con;
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.CommandText = outerObj.storedProcedureName;

                        object val = null;
                        foreach (string k in outerObj.propChangeHist.Keys)
                        {
                            val = outerObj.propChangeHist[k];

                            command.Parameters.Add(new SqlParameter("@ChangeDate", DateTime.Now));
                            command.Parameters.Add(new SqlParameter("@ObjectID", outerObj.objectID));
                            command.Parameters.Add(new SqlParameter("@PropertyID", outerObj.propertyIDMap[k]));
                            command.Parameters.Add(new SqlParameter("@PreviousValue", val.ToString()));
                            //command.Parameters.Add(new SqlParameter("@UserID", val));

                            resultCount = command.ExecuteNonQuery();

                            //If it doesn't save, and since we'll have nested transactions, we want to ensure that the update doesn't
                            //save if the change log entry doesn't save for whatever reason.
                            if (0 == resultCount)
                                throw new Exception("Failed to log change history for object: " + outerObj.objectID);
                        }
                    }

                    scope.Complete();
                }

            }
        }

        protected Dictionary<string, object> propChangeHist = null;
        protected string storedProcedureName = null;
        protected int objectID = -1;
        protected Dictionary<string, int> propertyIDMap = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="storedProcedure">Name of the stored procedure</param>
        /// <param name="objectID">ID for object/table based on another lookup table that enumerates these</param>
        /// <param name="propertyIDMap">A mapping between the name of the object's property and an ID based on an enumeration</param>
        public ObjectChangeHistoryManager(string storedProcedure, int objectID, Dictionary<string, int> propertyIDMap)
        {
            propChangeHist = new Dictionary<string, object>();
            this.storedProcedureName = storedProcedure;
            this.objectID = objectID;
            this.propertyIDMap = propertyIDMap;
        }

        public void AddChangedValue(string name, object originalValue)
        {
            //We're not concerned with subsequent, in memory, changes just the change from the
            //original value
            if (false == propChangeHist.ContainsKey(name))
                propChangeHist.Add(name, originalValue);

            //If originalValue is an object and not a primitive type then we might need to do a deep copy
            //on the object (?).
        }

        public void SaveHistory()
        {
            //Don't trap any exceptions here, since any object update will be a two phase save
            //and if the history logging failes then the object update should be rolled back
            new ObjectChangeHistoryManagerStorageHelper().saveChangeHistory(this);
        }
    }
    
    public class TransactionDetailEditable
    {
        
        public class TransactionDetailEditableStorageHelper
        {
            public void saveChanges(TransactionDetailEditable outerDetail)
            {
                using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required))
                {
                    //1. Save changes to TransactionDetail

                    //2. Save change history
                    outerDetail.changeManager.SaveHistory();

                    scope.Complete();
                }
            }
        }

        protected int orgID = 0;
        protected string messageToOrg = null;

        protected ObjectChangeHistoryManager changeManager = null;

        public TransactionDetailEditable()
        {
            Dictionary<string, int> propIDMap = new Dictionary<string, int>();
            propIDMap["OrgID"] = 0;
            propIDMap["MessageToOrg"] = 1;

            changeManager = new ObjectChangeHistoryManager("TransactionDetailLogChanges", 1, propIDMap);
        }

        public static TransactionDetailEditable getInstance(int transactionDetailID)
        {
            //Load TransactionDetail instance
            TransactionDetail t = new TransactionDetail();

            TransactionDetailEditable o = new TransactionDetailEditable { orgID = t.OrgID, messageToOrg = t.MessageToOrg };
            return o;
        }

        public void setOrgID(int orgID)
        {
            //delegate the management of change history
            changeManager.AddChangedValue("OrgID",this.orgID);
            this.orgID = orgID;
        }

        public void setMessageToOrg(string messageToOrg)
        {
            changeManager.AddChangedValue("MessageToOrg", this.messageToOrg);
            this.messageToOrg = messageToOrg;
        }

        public void save()
        {
            new TransactionDetailEditableStorageHelper().saveChanges(this);
        }

        
    }
    
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
}
