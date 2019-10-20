﻿using StreamCore.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class YouTubeConnection
    {
        internal static bool initialized = false;
        internal static void Initialize_Internal()
        {
            if (!initialized)
            {
                initialized = true;
                Task.Run(() =>
                {
                    Plugin.Log("Initializing!");

                    string ClientIDPath = Path.Combine(Globals.DataPath, "YouTubeClientId.json");
                    if (!File.Exists(ClientIDPath))
                    {
                        Plugin.Log("YouTubeClientId.json does not exist!");
                        // Client id/secret don't exist, abort
                        return;
                    }

                    // Initialize our client id/secret
                    if(!YouTubeOAuthToken.Initialize(ClientIDPath))
                    {
                        Plugin.Log("Something went wrong when trying to read YouTubeClientId.json.");
                        // Something went wrong when trying to read client id/secret
                        return;
                    }

                    string tokenPath = Path.Combine(Globals.DataPath, "YouTubeOAuthToken.json");
                    if (!File.Exists(tokenPath))
                    {
                        Plugin.Log("YouTubeOAuthToken.json does not exist, generating new auth token!");
                        // If we haven't already retrieved an oauth token, generate a new one
                        YouTubeOAuthToken.Generate();
                        return;
                    }

                    Plugin.Log("Auth token file exists!");

                    // Read our oauth token in from file
                    if (!YouTubeOAuthToken.Update(File.ReadAllText(tokenPath), false))
                    {
                        Plugin.Log("Failed to parse oauth token file, generating new auth token!");
                        // If we fail to parse the file, generate a new oauth token
                        YouTubeOAuthToken.Generate();
                        return;
                    }

                    Plugin.Log("Success parsing oauth token file!");

                    // Check if our auth key is expired, and if so refresh it
                    if (!YouTubeOAuthToken.Refresh())
                    {
                        Plugin.Log("Failed to refresh access token, generating new auth token!");
                        // If we fail to refresh our access token, generate a new one
                        YouTubeOAuthToken.Generate();
                        return;
                    }

                    // Sleep for a second before connecting, to allow for other plugins to register their callbacks
                    Thread.Sleep(1000);

                    // Finally, request our live broadcast info if everything went well
                    Start();
                });
            }
        }
        
        internal static void Start()
        {
            TaskHelper.ScheduleUniqueActionAtTime("YouTubeOAuthRefresh", () => YouTubeOAuthToken.Refresh(), YouTubeOAuthToken.expireTime.Subtract(new TimeSpan(0, 1, 0)));
            TaskHelper.ScheduleUniqueRepeatingAction("YouTubeChannelRefresh", () => YouTubeLiveBroadcast.Refresh(), 60000 * 3); // Refresh our list of broadcasts 3 minutes (this task will automatically cancel it's self once it latches onto a broadcast)
            TaskHelper.ScheduleUniqueRepeatingAction("YouTubeLiveChatRefresh", () => YouTubeLiveChat.Refresh(), 0);
        }

        internal static void Stop()
        {
            TaskHelper.CancelTask("YouTubeOAuthRefresh");
            TaskHelper.CancelTask("YouTubeChannelRefresh");
            TaskHelper.CancelTask("YouTubeLiveChatRefresh");
        }
    }
}
