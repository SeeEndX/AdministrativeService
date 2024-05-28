using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdminService
{
    public partial class FunctionExecutor
    {
        private Dictionary<string, Action> functionDictionary;

        public FunctionExecutor(Dictionary<string, Action> dictionary)
        {
            functionDictionary = dictionary;
        }
        
        //функция запуска функции по имени
        public void ExecuteMethodByName(string methodName)
        {
            if (functionDictionary.TryGetValue(methodName, out Action action))
            {
                action?.Invoke();
            }
        }
    }
}
