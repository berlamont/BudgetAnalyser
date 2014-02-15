﻿using BudgetAnalyser.Engine.Widget;
using GalaSoft.MvvmLight.Messaging;

namespace BudgetAnalyser.Dashboard
{
    public class WidgetActivatedMessage : MessageBase
    {
        public WidgetActivatedMessage(Widget widget)
        {
            Widget = widget;
        }

        public Widget Widget { get; private set; }
    }
}