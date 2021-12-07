using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace PgMigrationHelper.Helpers
{
    internal static class EnumHelpers
    {
        public static IEnumerable<KeyValuePair<T, string>> GetEnumItemsWithDescriptions<T>(Type enumType)
            where T : Enum
        {
            var actions = ((T[])Enum.GetValues(enumType)).ToList();
            var menuItems = new List<KeyValuePair<T, string>>();

            foreach (T action in actions)
            {
                MemberInfo[] memberInfos = enumType.GetMember(action.ToString());
                MemberInfo enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType);
                Attribute valueAttribute = enumValueMemberInfo.GetCustomAttribute(typeof(DescriptionAttribute), false);
                string description = ((DescriptionAttribute)valueAttribute).Description;

                menuItems.Add(new KeyValuePair<T, string>(action, description));
            }

            return menuItems;
        }
    }
}
