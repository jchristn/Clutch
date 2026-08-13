namespace Clutch.DataLoader
{
    /// <summary>
    /// A lightweight reference to a synthetic user (identifier, email, and display name) held by a tenant
    /// scope during data generation. Replaces the value tuple that previously carried these three fields.
    /// </summary>
    public sealed class UserRef
    {
        #region Public-Members

        /// <summary>
        /// The user identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The user email address.
        /// </summary>
        public string Email { get; }

        /// <summary>
        /// The user display name.
        /// </summary>
        public string Name { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a user reference.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        /// <param name="email">The user email address.</param>
        /// <param name="name">The user display name.</param>
        public UserRef(string id, string email, string name)
        {
            Id = id;
            Email = email;
            Name = name;
        }

        #endregion
    }
}
