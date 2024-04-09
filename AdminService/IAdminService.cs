using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;


namespace AdminService
{
    [ServiceContract]
    public interface IAdminService
    {
        [OperationContract]
        User Authenticate(string login, string password);
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
