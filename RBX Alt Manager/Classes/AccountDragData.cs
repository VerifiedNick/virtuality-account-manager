using System.Collections.Generic;

namespace RBX_Alt_Manager.Classes
{
    /// <summary>Carries the accounts being dragged from the account grid to a group in the sidebar. In-process only, never serialized.</summary>
    public class AccountDragData
    {
        public readonly List<Account> Accounts;

        public AccountDragData(List<Account> Accounts) => this.Accounts = Accounts;
    }
}
