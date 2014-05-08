﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using AddOne.Framework.Attribute;
using Castle.Core.Logging;
using AddOne.Framework.DAO;
using AddOne.Framework.Model.SAP;
using AddOne.Framework.Factory;

namespace AddOne.Framework.Service
{
    class AddInRunner
    {
        private string name;

        internal AddInRunner(string name)
        {
            this.name = name;
        }

        internal void Run()
        {
            var setup = new AppDomainSetup();
            setup.ApplicationName = "AddOne.Inception";
            setup.ApplicationBase = Environment.CurrentDirectory;

            AppDomain domain = AppDomain.CreateDomain("AddOne.AddIn", null, setup);
            domain.ExecuteAssembly(name + ".exe");
        }
    }

    internal class AddinLoader
    {

        public ILogger Logger { get; set; }

        private PermissionManager permissionManager;
        private BusinessOneDAO b1DAO;
        private BusinessOneUIDAO uiDAO;
        private EventDispatcher dispatcher;

        public AddinLoader(PermissionManager permissionManager, 
            BusinessOneDAO b1DAO, BusinessOneUIDAO uiDAO,
            EventDispatcher dispatcher)
        {
            this.permissionManager = permissionManager;
            this.b1DAO = b1DAO;
            this.uiDAO = uiDAO;
            this.dispatcher = dispatcher;
        }
        
        internal void LoadAddins(List<string> addins)
        {
            var authorizedAddins = FilterAuthorizedAddins(addins);
            foreach (var addin in authorizedAddins)
            {
                ConfigureAddin(addin);
                RegisterAddin(addin);
            }
        }

        private void ConfigureAddin(string addin)
        {
            Logger.Info(String.Format(Messages.ConfiguringAddin, addin));
            Assembly assembly;

            try
            {
                assembly = (from asm in AppDomain.CurrentDomain.GetAssemblies()
                                where asm.GetName().Name == addin
                                select asm).First();
            }
            catch (InvalidOperationException e)
            {
                Logger.Error(String.Format(Messages.AddInNotFound, addin), e);
                return;
            }

            var types = (from type in assembly.GetTypes()
                                 where type.IsClass
                                 select type);

            foreach (var type in types)
            {
                var attrs = type.GetCustomAttributes(true);
                foreach (var attr in attrs)
                {
                    Logger.Debug(String.Format(Messages.ProcessingAttribute, attr, type));
                    if (attr is ResourceBOMAttribute)
                    {
                        ProcessAddInAttribute((ResourceBOMAttribute)attr);
                    }
                    else if (attr is PermissionAttribute)
                    {
                        ProcessPermissionAttribute((PermissionAttribute)attr);
                    }
                }
            }
        }

        private void ProcessPermissionAttribute(PermissionAttribute permissionAttribute)
        {
            b1DAO.UpdateOrSavePermissionIfNotExists(permissionAttribute);
        }

        private void ProcessAddInAttribute(ResourceBOMAttribute resourceBOMAttribute)
        {
            try
            {
                using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceBOMAttribute.ResourceName))
                {
                    switch (resourceBOMAttribute.Type)
                    {
                        case ResourceType.UserField:
                            var userFieldBOM = b1DAO.GetBOMFromXML<UserFieldBOM>(resourceStream);
                            b1DAO.SaveBOMIfNotExists(userFieldBOM);
                            break;
                        case ResourceType.UserTable:
                            var userTableBOM = b1DAO.GetBOMFromXML<UserTableBOM>(resourceStream);
                            b1DAO.SaveBOMIfNotExists(userTableBOM);
                            break;
                        case ResourceType.UDO:
                            var udoBOM = b1DAO.GetBOMFromXML<UDOBOM>(resourceStream);
                            b1DAO.UpdateOrSaveBOMIfNotExists(udoBOM);
                            break;
                        case ResourceType.FormattedSearch:
                            var fsBOM = b1DAO.GetBOMFromXML<FormattedSearchBOM>(resourceStream);
                            b1DAO.UpdateOrSaveBOMIfNotExists(fsBOM);
                            break;
                        case ResourceType.QueryCategories:
                            var qcBOM = b1DAO.GetBOMFromXML<QueryCategoriesBOM>(resourceStream);
                            b1DAO.UpdateOrSaveBOMIfNotExists(qcBOM);
                            break;
                        case ResourceType.UserQueries:
                            var uqBOM = b1DAO.GetBOMFromXML<UserQueriesBOM>(resourceStream);
                            b1DAO.UpdateOrSaveBOMIfNotExists(uqBOM);
                            break;
                    }
                }

            }
            catch (Exception e)
            {
                Logger.Error(String.Format("Não foi possível processar atributo {0} do Addin.", resourceBOMAttribute), e);
            }
        }

        private void RegisterAddin(string addin)
        {
            AddInRunner runner = new AddInRunner(addin);
            var thread = new Thread(new ThreadStart(runner.Run));
            thread.Start();
        }

        private List<string> FilterAuthorizedAddins(List<string> addins)
        {
            List<string> authorized = new List<string>();
            foreach (string addin in addins)
            {
                if (permissionManager.AddInEnabled(addin))
                    authorized.Add(addin);
            }
            return authorized;
        }

        internal void StartThis()
        {
            try
            {
                var addin = Assembly.GetEntryAssembly().FullName;
                Logger.Info(String.Format(Messages.ConfiguringAddin, addin));
                List<MenuAttribute> menus = new List<MenuAttribute>();
                var assembly = Assembly.GetEntryAssembly();
                var types = (from type in assembly.GetTypes()
                             where type.IsClass
                             select type);

                foreach (var type in types)
                {
                    var attrs = type.GetCustomAttributes(true);
                    ProcessAddInStartupAttribute(attrs, type);
                    foreach (var method in type.GetMethods())
                    {
                        attrs = method.GetCustomAttributes(true);
                        ProcessAddInStartupAttribute(attrs, type);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(Messages.StartThisError, e);
            }
        }

        private void ProcessAddInStartupAttribute(object[] attrs, Type type)
        {
            List<MenuAttribute> menus = new List<MenuAttribute>();

            foreach (var attr in attrs)
            {
                Logger.Debug(String.Format(Messages.ProcessingAttribute, attr, type));
                if (attr is MenuEventAttribute)
                {
                    ((MenuEventAttribute)attr).OriginalType = type;
                    dispatcher.RegisterMenuEvent((MenuEventAttribute)attr);
                }
                else if (attr is MenuAttribute)
                {
                    ((MenuAttribute)attr).OriginalType = type;
                    menus.Add((MenuAttribute)attr);
                }
            }
            uiDAO.ProcessMenuAttribute(menus);
        }

    }
}