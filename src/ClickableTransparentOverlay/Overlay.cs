﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using Coroutine;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace ClickableTransparentOverlay
{
    // TODO: Implement overlay info, warn, error logger.
    /// <summary>
    /// A class to create clickable transparent overlay.
    /// </summary>
    public static class Overlay
    {
        private readonly static Sdl2Window window;
        private readonly static GraphicsDevice graphicsDevice;
        private readonly static CommandList commandList;
        private readonly static ImGuiController imController;
        private readonly static Vector4 clearColor;
        private readonly static Dictionary<string, Texture> loadedImages;
        private static bool terminal = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="Overlay"/> class.
        /// </summary>
        static Overlay()
        {
            clearColor = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            loadedImages = new Dictionary<string, Texture>();
            window = new Sdl2Window(
                "Overlay",
                0,
                0,
                (2560*3), // 2560
                1440, // 1440
                SDL_WindowFlags.Borderless |
                    SDL_WindowFlags.AlwaysOnTop |
                    SDL_WindowFlags.SkipTaskbar,
                false);
            graphicsDevice = VeldridStartup.CreateDefaultD3D11GraphicsDevice(
                new GraphicsDeviceOptions(false, null, true),
                window);
            commandList = graphicsDevice.ResourceFactory.CreateCommandList();
            imController = new ImGuiController(
                graphicsDevice,
                graphicsDevice.MainSwapchain.Framebuffer.OutputDescription,
                window.Width,
                window.Height);
            window.Resized += () =>
            {
                graphicsDevice.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
                imController.WindowResized(window.Width, window.Height);
            };

            NativeMethods.InitTransparency(window.Handle);
            NativeMethods.SetOverlayClickable(window.Handle, false);
        }

        /// <summary>
        /// Infinitely renders the over (and execute co-routines) till it's closed.
        /// </summary>
        public static void RunInfiniteLoop()
        {
            DateTime previous = DateTime.Now;
            DateTime current;
            TimeSpan interval;
            float sec;
            while (window.Exists && !Close)
            {
                InputSnapshot snapshot = window.PumpEvents();
                if (!window.Exists) { break; }
                current = DateTime.Now;
                interval = current - previous;
                sec = (float)interval.TotalSeconds;
                previous = current;
                imController.Update(sec > 0 ? sec : 0.001f, snapshot, window.Handle);
                CoroutineHandler.Tick(interval.TotalSeconds);
                if (Visible)
                {
                    CoroutineHandler.RaiseEvent(OnRender);
                }

                commandList.Begin();
                commandList.SetFramebuffer(graphicsDevice.MainSwapchain.Framebuffer);
                commandList.ClearColorTarget(0, new RgbaFloat(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W));
                imController.Render(graphicsDevice, commandList);
                commandList.End();
                graphicsDevice.SubmitCommands(commandList);
                graphicsDevice.SwapBuffers(graphicsDevice.MainSwapchain);
            }

            Dispose();
        }

        /// <summary>
        /// To submit ImGui code for generating the UI.
        /// </summary>
        public static Event OnRender = new Event();

        /// <summary>
        /// Safely Closes the Overlay.
        /// Doesn't matter if you set it to true multiple times.
        /// </summary>
        public static bool Close { get; set; } = false;

        /// <summary>
        /// Makes the overlay visible or invisible. Invisible Overlay
        /// will not call OnRender coroutines, however time based
        /// coroutines are still called.
        /// </summary>
        public static bool Visible { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to hide the terminal window.
        /// </summary>
        public static bool TerminalWindow
        {
            get => terminal;
            set
            {
                if (value != terminal)
                {
                    NativeMethods.SetConsoleWindow(value);
                }

                terminal = value;
            }
        }

        /// <summary>
        /// Gets or sets the position of the overlay window.
        /// </summary>
        public static Point Position
        {
            get
            {
                return new Point(window.X, window.Y);
            }
            set
            {
                Sdl2Native.SDL_SetWindowPosition(window.SdlWindowHandle, value.X, value.Y);
            }
        }

        /// <summary>
        /// Gets the number of displays available on the computer.
        /// </summary>
        public static int NumberVideoDisplays
        {
            get
            {
                return Sdl2Native.SDL_GetNumVideoDisplays();
            }
        }

        /// <summary>
        /// Gets the monitor bounds based on the monitor number.
        /// </summary>
        /// <param name="num">Monitor number starting from 0.</param>
        /// <returns>screen box in which the window is moved to.</returns>
        public static Rectangle GetDisplayBounds(int num)
        {
            int numDisplays = NumberVideoDisplays;
            if ( num >= numDisplays || num < 0)
            {
                return new Rectangle(Position, Size);
            }

            var bounds = new Rectangle();
            SDL2Functions.SDL_GetDisplayBounds(num, ref bounds);
            return bounds;
        }

        /// <summary>
        /// Gets or sets the size of the overlay window.
        /// </summary>
        public static Point Size
        {
            get
            {
                return new Point(window.Width, window.Height);
            }
            set
            {
                Sdl2Native.SDL_SetWindowSize(window.SdlWindowHandle, value.X, value.Y);
            }
        }

        /// <summary>
        /// Adds the image to the Graphic Device as a texture.
        /// Then returns the pointer of the added texture. It also
        /// cache the image internally rather than creating a new texture on every call,
        /// so this function can be called multiple times per frame.
        /// </summary>
        /// <param name="filePath">
        /// Path to the image on disk. If the image is loaded in the memory
        /// save it on the disk before sending to this function. Reason for this
        /// is to cache the Image Texture using filePath as the key.
        /// </param>
        /// <param name="handle">output pointer to the image in the graphic device.</param>
        /// <param name="width">width of the loaded image.</param>
        /// <param name="height">height of the loaded image.</param>
        public static void AddOrGetImagePointer(
            string filePath,
            out IntPtr handle,
            out uint width,
            out uint height)
        {
            if (!loadedImages.TryGetValue(filePath, out Texture texture))
            {
                ImageSharpTexture imgSharpTexture = new ImageSharpTexture(filePath);
                texture = imgSharpTexture.CreateDeviceTexture(graphicsDevice, graphicsDevice.ResourceFactory);
                loadedImages.Add(filePath, texture);
            }

            width = texture.Width;
            height = texture.Height;
            handle = imController.GetOrCreateImGuiBinding(graphicsDevice.ResourceFactory, texture);
        }

        /// <summary>
        /// 스트림에 이미지를 태그로 추가함
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="tag"></param>
        /// <param name="handle"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public static void AddOrGetImagePointerStream(
           Stream stream,
           string tag,
           out IntPtr handle,
           out uint width,
           out uint height)
        {
            if (!loadedImages.TryGetValue(tag, out Texture texture))
            {
                stream.Position = 0;
                ImageSharpTexture imgSharpTexture = new ImageSharpTexture(stream);
                texture = imgSharpTexture.CreateDeviceTexture(graphicsDevice, graphicsDevice.ResourceFactory);
                loadedImages.Add(tag, texture);
            }

            width = texture.Width;
            height = texture.Height;
            handle = imController.GetOrCreateImGuiBinding(graphicsDevice.ResourceFactory, texture);
        }


        /// <summary>
        /// Free all resources acquired by the overlay.
        /// </summary>
        private static void Dispose()
        {
            window.Close();
            while (window.Exists)
            {
                Thread.Sleep(1);
            }

            graphicsDevice.WaitForIdle();
            imController.Dispose();
            commandList.Dispose();
            graphicsDevice.WaitForIdle();
            graphicsDevice.Dispose();
            loadedImages.Clear();
            NativeMethods.SetConsoleWindow(true);
        }
    }
}