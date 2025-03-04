﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Android.Content;
using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using BmwFileReader;
using EdiabasLib;

namespace BmwDeepObd
{
    public class ArgAssistBaseActivity : BaseActivity, View.IOnTouchListener
    {
        public delegate void AcceptDelegate(bool accepted);

        public const string ArgTypeArg = "ARG";
        public const string ArgTypeID = "ID";

        // Intent extra
        public const string ExtraServiceId = "service_id";
        public const string ExtraOffline = "offline";
        public const string ExtraArguments = "arguments";
        public const string ExtraExecute = "execute";

        public static List<SgFunctions.SgFuncInfo> IntentSgFuncInfo { get; set; }

        protected InputMethodManager _imm;
        protected View _contentView;
        protected View _barView;
        protected ActivityCommon _activityCommon;

        protected int _serviceId;
        protected bool _offline;
        protected Button _buttonApply;
        protected Button _buttonExecute;
        protected TextView _textViewArgTypeTitle;
        protected RadioButton _radioButtonArgTypeArg;
        protected RadioButton _radioButtonArgTypeId;
        protected List<SgFunctions.SgFuncInfo> _sgFuncInfoList;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            SetTheme(ActivityCommon.SelectedThemeId);
            base.OnCreate(savedInstanceState);

            if (IntentSgFuncInfo == null)
            {
                Finish();
                return;
            }
        }

        protected void InitBaseVariables()
        {
            _imm = (InputMethodManager)GetSystemService(InputMethodService);
            _contentView = FindViewById<View>(Android.Resource.Id.Content);

            _barView = LayoutInflater.Inflate(Resource.Layout.bar_arg_assist, null);
            ActionBar.LayoutParams barLayoutParams = new ActionBar.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            barLayoutParams.Gravity = barLayoutParams.Gravity &
                                      (int)(~(GravityFlags.HorizontalGravityMask | GravityFlags.VerticalGravityMask)) |
                                      (int)(GravityFlags.Left | GravityFlags.CenterVertical);
            SupportActionBar.SetCustomView(_barView, barLayoutParams);

            SetResult(Android.App.Result.Canceled);

            _serviceId = Intent.GetIntExtra(ExtraServiceId, -1);
            _offline = Intent.GetBooleanExtra(ExtraOffline, false);

            _activityCommon = new ActivityCommon(this);

            _sgFuncInfoList = IntentSgFuncInfo;

            _buttonApply = _barView.FindViewById<Button>(Resource.Id.buttonApply);
            _buttonApply.SetOnTouchListener(this);

            _buttonExecute = _barView.FindViewById<Button>(Resource.Id.buttonExecute);
            _buttonExecute.SetOnTouchListener(this);

            _textViewArgTypeTitle = FindViewById<TextView>(Resource.Id.textViewArgTypeTitle);
            _textViewArgTypeTitle.SetOnTouchListener(this);

            _radioButtonArgTypeArg = FindViewById<RadioButton>(Resource.Id.radioButtonArgTypeArg);
            _radioButtonArgTypeArg.SetOnTouchListener(this);

            _radioButtonArgTypeId = FindViewById<RadioButton>(Resource.Id.radioButtonArgTypeId);
            _radioButtonArgTypeId.SetOnTouchListener(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _activityCommon?.Dispose();
            _activityCommon = null;
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            switch (e.Action)
            {
                case MotionEventActions.Down:
                    HideKeyboard();
                    break;
            }
            return false;
        }

        protected void HideKeyboard()
        {
            _imm?.HideSoftInputFromWindow(_contentView.WindowToken, HideSoftInputFlags.None);
        }
    }
}
