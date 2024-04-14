﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Data;


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
