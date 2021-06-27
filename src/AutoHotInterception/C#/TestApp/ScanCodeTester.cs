﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoHotInterception;
using AutoHotInterception.Helpers;

namespace TestApp
{
    public class ScanCodeTester
    {
        public ScanCodeTester()
        {
            var scc = new ScanCodeChecker(); 
            scc.Subscribe(2131, 326, new Action<KeyEvent[]>(OnKeyEvent));
        }

        public void OnKeyEvent(KeyEvent[] keyEvents)
        {
            var str = "";
            foreach (var keyEvent in keyEvents)
            {
                str += $"Code: {keyEvent.Code}, State: {keyEvent.State} | ";
            }
            Console.WriteLine(str);
        }
    }
}
