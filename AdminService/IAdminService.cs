﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Data;
using Microsoft.Web.Administration;


namespace AdminService
{
    [ServiceContract]
    public interface IAdminService
    {
        [OperationContract]
        User Authenticate(string login, string password);

        [OperationContract]
        DataTable GetUsersData();

        [OperationContract]
        int AddUser(string username, string password);

        [OperationContract]
        int EditUser(string originalUsername, string newUsername, string newPassword);

        [OperationContract]
        void DeleteUser(int userId);

        [OperationContract]
        int GetSelectedUserId(string newUsername);

        [OperationContract]
        void UpdatePass(int userId, string newPassword);

        [OperationContract]
        List<string> GetFunctions();

        [OperationContract]
        bool IsUserExists(string login);

        [OperationContract]
        List<string> GetAllFunctionNames();

        [OperationContract]
        List<string> GetAssignedFunctionNames(string username);

        [OperationContract]
        void SaveAssignedFunctions(string username, List<string> selectedFunctionNames);

        [OperationContract]
        (List<string>, string) GetReportsForUser(int user);

        [OperationContract]
        List<IISManager.SiteInfo> GetListOfSites();

        [OperationContract]
        List<IISManager.AppPoolInfo> GetListOfAppPools();

        [OperationContract]
        void StartSite(string siteName);

        [OperationContract]
        void StopSite(string siteName);

        [OperationContract]
        void CreateWebsite(string siteName, string physicalPath, int port);

        [OperationContract]
        void DeleteWebsite(string siteName);

        [OperationContract]
        void ModifyWebsite(string currentSiteName, string newSiteName, string newPhysicalPath);

        [OperationContract]
        void CreatePool(string poolName, ManagedPipelineMode mode, int memoryLimit, int intervalMinutes);

        [OperationContract]
        void DeletePool(string poolName);

        [OperationContract]
        void ModifyPool(string currentPoolName, string newPoolName, ManagedPipelineMode mode, int memoryLimit, int intervalMinutes);

        [OperationContract]
        void StartAppPool(string appPoolName);

        [OperationContract]
        void StopAppPool(string appPoolName);

        [OperationContract]
        void AddReport(string currentUser, string description);
    }

    [DataContract]
    public class User
    {
        [DataMember]
        public string Login { get; set; }

        [DataMember]
        public string Pass { get; set; }

        [DataMember]
        public string Group { get; set; }
    }
}
