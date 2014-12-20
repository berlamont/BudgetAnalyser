using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using BudgetAnalyser.Engine.Account;
using BudgetAnalyser.Engine.Annotations;
using BudgetAnalyser.Engine.Budget;
using BudgetAnalyser.Engine.Widgets;

namespace BudgetAnalyser.Engine.Services
{
    public interface IDashboardService : IServiceFoundation
    {
        /// <summary>
        ///     Creates a new bucket monitor widget and adds it to the tracked widgetGroups collection.
        ///     Duplicates are not allowed in the collection and will not be added.
        /// </summary>
        /// <param name="bucketCode">The bucket code to create a new monitor widget for.</param>
        /// <returns>
        ///     Will return a reference to the newly created widget, or null if the widget was not created because a duplicate
        ///     already exists.
        /// </returns>
        Widget CreateNewBucketMonitorWidget(string bucketCode);

        /// <summary>
        ///     Creates the new fixed budget monitor widget. Also creates all supporting background infrastructure to support the
        ///     project including a sub-class
        ///     of Surplus.
        /// </summary>
        /// <param name="bucketCode">
        ///     The code to use for a <see cref="BudgetBucket" /> bucket code. This will be a bucket that
        ///     inherits from Surplus.
        /// </param>
        /// <param name="description">The description.</param>
        /// <param name="fixedBudgetAmount">The fixed budget amount.</param>
        /// <exception cref="ArgumentException">Will be thrown if the bucket code already exists.</exception>
        Widget CreateNewFixedBudgetMonitorWidget([NotNull] string bucketCode, [NotNull] string description, decimal fixedBudgetAmount);

        /// <summary>
        ///     Retrieves a list of filterable account types for use on the dashboard, (to select an account type to filter by).
        /// </summary>
        IEnumerable<AccountType> FilterableAccountTypes();

        /// <summary>
        ///     Initialises and returns the widget groups to view in the UI.
        ///     This must be called first before other methods of this service can be used.
        ///     The collection of widget groups is cached inside the service for use by the other methods.
        /// </summary>
        ObservableCollection<WidgetGroup> LoadPersistedStateData(IEnumerable<WidgetPersistentState> storedState);

        /// <summary>
        ///     Notifies the service that a dependency has been changed in the UI and all dependent widgets should be updated.
        /// </summary>
        /// <typeparam name="T">The type to register if not the actual concrete type.</typeparam>
        [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "Preferred method of passing type parameter")]
        void NotifyOfDependencyChange<T>([CanBeNull] object dependency);

        /// <summary>
        ///     Notifies the service that a dependency has been changed in the UI and all dependent widgets should be updated.
        /// </summary>
        void NotifyOfDependencyChange([CanBeNull] object dependency);

        /// <summary>
        ///     Prepares the persistent data for saving into permenant storage.
        /// </summary>
        IEnumerable<WidgetPersistentState> PreparePersistentStateData();

        /// <summary>
        ///     Removes a multi-instance widget from the widget groups.
        /// </summary>
        /// <param name="widgetToRemove">The widget to remove.</param>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Multi", Justification = "Preferred spelling")]
        void RemoveUserDefinedWidget(IUserDefinedWidget widgetToRemove);

        /// <summary>
        ///     Makes all widgets visible.
        /// </summary>
        void ShowAllWidgets();
    }
}