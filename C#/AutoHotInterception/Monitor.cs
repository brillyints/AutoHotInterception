﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoHotInterception.Helpers;
using static AutoHotInterception.Helpers.HelperFunctions;

namespace AutoHotInterception
{
    public class Monitor : IDisposable
    {
        private readonly IntPtr _deviceContext;
        private Thread _pollThread;
        private bool _pollThreadRunning = false;
        private dynamic _keyboardCallback;
        private dynamic _mouseCallback;
        private bool _filterState = false;
        private readonly ConcurrentDictionary<int, bool> _filteredDevices = new ConcurrentDictionary<int, bool>();

        public Monitor()
        {
            _deviceContext = ManagedWrapper.CreateContext();
            SetThreadState(true);
        }

        public string OkCheck()
        {
            return "OK";
        }

        //public void Log(string text)
        //{
        //    Debug.WriteLine($"AHK| {text}");
        //}

        public void Subscribe(dynamic keyboardCallback, dynamic mouseCallback)
        {
            _keyboardCallback = keyboardCallback;
            _mouseCallback = mouseCallback;
        }

        public bool SetDeviceFilterState(int device, bool state)
        {
            SetFilterState(false);
            if (state)
            {
                _filteredDevices[device] = true;
                //Log($"Adding device {device}, count: {_filteredDevices.Count}");
            }
            else
            {
                _filteredDevices.TryRemove(device, out _);
                //Log($"Removing device {device}, count: {_filteredDevices.Count}");
            }

            if (_filteredDevices.Count > 0)
            {
                SetFilterState(true);
            }
            return true;
        }

        public DeviceInfo[] GetDeviceList()
        {
            return HelperFunctions.GetDeviceList(_deviceContext);
        }

        private void SetFilterState(bool state)
        {
            ManagedWrapper.SetFilter(_deviceContext, IsMonitoredDevice,
                state ? ManagedWrapper.Filter.All : ManagedWrapper.Filter.None);
            _filterState = state;
        }

        private int IsMonitoredDevice(int device)
        {
            return Convert.ToInt32(_filteredDevices.ContainsKey(device));
        }


        private void SetThreadState(bool state)
        {
            if (state)
            {
                if (_pollThreadRunning) return;

                _pollThreadRunning = true;
                _pollThread = new Thread(PollThread);
                _pollThread.Start();
            }
            else
            {
                _pollThread.Abort();
                _pollThread.Join();
                _pollThread = null;
            }
        }

        private void PollThread()
        {
            var stroke = new ManagedWrapper.Stroke();

            while (true)
            {
                for (var i = 1; i < 11; i++)
                {
                    while (ManagedWrapper.Receive(_deviceContext, i, ref stroke, 1) > 0)
                    {
                        ManagedWrapper.Send(_deviceContext, i, ref stroke, 1);
                        FireKeyboardCallback(i, stroke);
                    }
                }

                for (var i = 11; i < 21; i++)
                {
                    while (ManagedWrapper.Receive(_deviceContext, i, ref stroke, 1) > 0)
                    {
                        ManagedWrapper.Send(_deviceContext, i, ref stroke, 1);
                        FireMouseCallback(i, stroke);
                    }
                }
                Thread.Sleep(10);
            }
        }

        private void FireKeyboardCallback(int id, ManagedWrapper.Stroke stroke)
        {
            ThreadPool.QueueUserWorkItem(threadProc => _keyboardCallback(id, stroke.key.state, stroke.key.code, stroke.key.information));
        }

        private void FireMouseCallback(int id, ManagedWrapper.Stroke stroke)
        {
            ThreadPool.QueueUserWorkItem(threadProc => _mouseCallback(id, stroke.mouse.state, stroke.mouse.flags, stroke.mouse.rolling, stroke.mouse.x, stroke.mouse.y, stroke.mouse.information));
        }

        public void Dispose()
        {
            SetFilterState(false);
            SetThreadState(false);
        }
    }
}
