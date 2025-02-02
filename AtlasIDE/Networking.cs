﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AtlasIDE
{
    class Networking
    {
        private static UdpClient udpClient;
        public static List<Thing> Things { get; } = new List<Thing>();
        public static List<Service> Services { get; } = new List<Service>();
        public static ObservableCollection<Service> ServicesCollection { get; } = new ObservableCollection<Service>();
        public static ObservableCollection<Relationship> RelationshipCollection { get; } = new ObservableCollection<Relationship>();
        public static MainWindow Window;

        public static void Start()
        {
            udpClient = new UdpClient(1235);
            udpClient.JoinMulticastGroup(IPAddress.Parse("232.1.1.1"), 16);

            var receiveThread = new Thread(Receive);
            receiveThread.Start();            
        }

        public static void Receive()
        {
            while (true)
            {
                var ipEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = udpClient.Receive(ref ipEndPoint);

                var Message = Encoding.Default.GetString(data);

                var tweet = JsonConvert.DeserializeObject<Tweet>(Message);
                switch (tweet.TweetType)
                {
                    case "Identity_Thing":
                        var thingTweet = JsonConvert.DeserializeObject<IdentityThingTweet>(Message);
                        if (Things.Find(x => x.ID == tweet.ThingID) == null)
                        {
                            Console.WriteLine("New thing found!");
                            Things.Add(new Thing(thingTweet));
                            Application.Current.Dispatcher.Invoke(Window.UpdateThings, DispatcherPriority.ContextIdle);
                        }
                        break;
                    case "Identity_Language":
                        var langTweet = JsonConvert.DeserializeObject<IdentityLanguageTweet>(Message);
                        var thing = Things.Find(x => x.ID == langTweet.ThingID);
                        if (thing != null)
                        {
                            thing.AddNetworkInfo(langTweet);
                            Application.Current.Dispatcher.Invoke(Window.UpdateThings, DispatcherPriority.ContextIdle);
                        }
                        break;
                    case "Identity_Entity":
                        var entityTweet = JsonConvert.DeserializeObject<IdentityEntityTweet>(Message);
                        thing = Things.Find(x => x.ID == entityTweet.ThingID);
                        if (thing == null) break;

                        // Seach for entities and add if it doesn't exist
                        if (thing.Entities.Find(x => x.ID == entityTweet.ID) == null)
                        {
                            Console.WriteLine("New entity found!");
                            thing.Entities.Add(new Entity(entityTweet));
                            Application.Current.Dispatcher.Invoke(Window.UpdateThings, DispatcherPriority.ContextIdle);
                        }
                        break;
                    case "Service":
                        var serviceTweet = JsonConvert.DeserializeObject<ServiceTweet>(Message);
                        thing = Things.Find(x => x.ID == serviceTweet.ThingID);
                        if (thing == null) break;

                        var entity = thing.Entities.Find(x => x.ID == serviceTweet.EntityID);
                        if (entity == null) break;

                        // Seach for services and add if it doesn't exist
                        if (entity.Services.Find(x => x.Name == serviceTweet.Name) == null)
                        {
                            Console.WriteLine("New service found!");
                            var service = new Service(serviceTweet);
                            entity.Services.Add(service);
                            Services.Add(service);
                            Application.Current.Dispatcher.Invoke((Action)delegate
                            {
                                ServicesCollection.Add(service);
                            });
                            Application.Current.Dispatcher.Invoke(Window.UpdateServices, DispatcherPriority.ContextIdle);
                        }
                        break;

                    case "Relationship":
                        var relationshipTweet = JsonConvert.DeserializeObject<RelationshipTweet>(Message);
                        thing = Things.Find(x => x.ID == relationshipTweet.ThingID);
                        if (thing == null) break;

                        // Seach for relationships and add if it doesn't exist
                        if (thing.Relationships.Find(x => x.Name == relationshipTweet.Name) == null)
                        {
                            Console.WriteLine("New relationship found!");
                            thing.Relationships.Add(new Relationship(relationshipTweet));

                            Application.Current.Dispatcher.Invoke(Window.UpdateRelationship, DispatcherPriority.ContextIdle);
                        }
                        break;

                    default:
                        break;
                }
            }
        }


        private static readonly string HOST = "192.168.0.199";
        private static readonly string HOST2 = "192.168.0.176";
        private static readonly int PORT = 6668;
        public static ServiceResponseTweet Call(Service service, string input)
        {
            ServiceCallTweet call = new ServiceCallTweet();
            call.TweetType = "Service call";
            call.ThingID = service.ThingID;
            call.SpaceID = service.SpaceID;
            call.Name = service.Name;
            call.Inputs = '(' + input.ToString() + ')';
            Console.WriteLine(JsonConvert.SerializeObject(call, Formatting.Indented));

            Thing thing = Things.Find(x => x.ID == service.ThingID);

            TcpClient client;
            if(service.ThingID == "Kyles_RPi4")
                client = new TcpClient(HOST, PORT);
            else
                client = new TcpClient(HOST2, PORT);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(call, Formatting.Indented));
            NetworkStream stream = client.GetStream();
            stream.Write(data, 0, data.Length);

            data = new Byte[256];
            String responseData = String.Empty;
            Int32 bytes = stream.Read(data, 0, data.Length);
            responseData = Encoding.ASCII.GetString(data, 0, bytes);
            ServiceResponseTweet response = JsonConvert.DeserializeObject<ServiceResponseTweet>(responseData);
            Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.Indented));
            stream.Close();
            client.Close();


            //Console.WriteLine(response.ServiceName + ": " + response.ServiceResult);
            //Outputs.Add(response.ServiceName + ": " + response.ServiceResult);
            return response;
        }

        public static void VoidCall(Service service, ref ServiceResponseTweet response)
        {
            ServiceCallTweet call = new ServiceCallTweet();
            call.TweetType = "Service call";
            call.ThingID = service.ThingID;
            call.SpaceID = service.SpaceID;
            call.Name = service.Name;
            call.Inputs = "()";
            Console.WriteLine(JsonConvert.SerializeObject(call, Formatting.Indented));

            Thing thing = Things.Find(x => x.ID == service.ThingID);

            TcpClient client = new TcpClient(HOST, PORT);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(call, Formatting.Indented));
            NetworkStream stream = client.GetStream();
            stream.Write(data, 0, data.Length);

            data = new Byte[256];
            String responseData = String.Empty;
            Int32 bytes = stream.Read(data, 0, data.Length);
            responseData = Encoding.ASCII.GetString(data, 0, bytes);
            response = JsonConvert.DeserializeObject<ServiceResponseTweet>(responseData);
            Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.Indented));
            stream.Close();
            client.Close();
        }

        public static ServiceResponseTweet EvalauteRelationship(Relationship relationship)
        {
            Service first = findService(relationship.FSname);
            Service second = findService(relationship.SSname);
            String type = relationship.Type;

            if (first == null || second == null)
            {
                return null;
            }

            ServiceResponseTweet firstResponseTweet = new ServiceResponseTweet();
            ServiceResponseTweet serviceResponseTweet = new ServiceResponseTweet();

            if (type.Equals("control") || type.Equals("System.Windows.Controls.ComboBoxItem: control"))
            {
                firstResponseTweet = Call(first, relationship.FSargs); // First call to obtain input for second call

                String serviceResponse = firstResponseTweet.ServiceResult;

                bool anIntegerNumber = int.TryParse(serviceResponse, out int boolRes);

                if (!anIntegerNumber)
                {
                    return null;
                }



                if (Convert.ToBoolean(boolRes))
                {
                    serviceResponseTweet = Call(second, relationship.SSargs);
                }
                else
                {
                    serviceResponseTweet.ServiceName = second.Name;
                    serviceResponseTweet.ServiceResult = "First Service evaluated to 0, " + second.Name + " did not run!";
                }


            }
            else if (type.Equals("drive") || type.Equals("System.Windows.Controls.ComboBoxItem: drive"))
            {
                firstResponseTweet = Call(first, relationship.FSargs); // First call to obtain input for second call

                serviceResponseTweet = Call(second, firstResponseTweet.ServiceResult);

            }
            //else if (type.Equals("Support")) // Before Service 1, Check on Service 2
            //{

            //}
            else if (type.Equals("extend") || type.Equals("System.Windows.Controls.ComboBoxItem: extend")) // Do Service 1 While Doing Service 2
            {
                //firstResponseTweet = new ServiceResponseTweet();
                //serviceResponseTweet = new ServiceResponseTweet();

                var doThread = new Thread(() => firstResponseTweet = Call(first, relationship.FSargs));
                var whileDoingThread = new Thread(() => serviceResponseTweet = Call(second, relationship.SSargs));
                doThread.Start();
                whileDoingThread.Start();

            }
            //else if (type.Equals("Contest")) // Prefer using Service 1 over Service 2
            //{

            //}
            //else if (type.Equals("Interfere")) // Do not do service 1 if doing service 2
            //{

            //}
            else
            {
                serviceResponseTweet = null;
            }

            if (serviceResponseTweet != null) { 
                Console.WriteLine(serviceResponseTweet.ServiceName + ": " + serviceResponseTweet.ServiceResult);
                //Outputs.Add(serviceResponseTweet.ServiceName + ": " + serviceResponseTweet.ServiceResult); 
            }
            return serviceResponseTweet;

        }

        public static Service findService(String name)
        {
            foreach (Service service in Services)
            {
                if (service.Name.Equals(name))
                    return service;
            }
            return null;
        }

    }
}