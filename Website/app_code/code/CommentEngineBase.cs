﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Compilation;
using System.Web.WebPages;
using System.Xml.Linq;

public abstract class CommentEngineBase : ICommentEngine
{
    private string _globalSectionPath;
    private string _commentSectionPath;
    private string _commentCountSectionPath;
    private static ConcurrentDictionary<string, string> _settings = new ConcurrentDictionary<string, string>(); 

    /// <summary>
    /// Name of the comment engine
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Path to the comment section.
    /// Convention is: ~/views/commentengine/{name}/{name}.cshtml
    /// </summary>
    public virtual string CommentSectionPath
    {
        get
        {
            return _commentSectionPath ??
                   (_commentSectionPath = string.Format("~/views/commentengines/{0}/{0}.cshtml", Name));
        }
        set
        {
            _commentSectionPath = value; 
            
        }
    }

    /// <summary>
    /// Path to the comment count section.
    /// Convention is: ~/views/commentengine/{name}/commentcount.cshtml
    /// </summary>
    public virtual string CommentCountSectionPath
    {
        get
        {
            return _commentCountSectionPath ??
                   (_commentCountSectionPath = string.Format("~/views/commentengines/{0}/commentcount.cshtml", Name));
        }
        set
        {
            _commentCountSectionPath = value; 
            
        }
    }

    /// <summary>
    /// Path to the global section
    /// Convention is: ~/views/commentengine/{name}/global.cshtml
    /// </summary>
    public string GlobalSectionPath
    {
        get {
            return _globalSectionPath ??
                   (_globalSectionPath = string.Format("~/views/commentengines/{0}/global.cshtml", Name));
        }
        set
        {
            _globalSectionPath = value;
            
        }
    }

    /// <summary>
    /// Method to render the comments section
    /// </summary>
    /// <param name="post">The current post</param>
    /// <param name="context">The context</param>
    /// <returns><see cref="HelperResult"/></returns>
    public virtual HelperResult RenderCommentSection(Post post, HttpContext context)
    {
        var contextWrapper = new HttpContextWrapper(context);
        return RenderHelperResult(CommentSectionPath, new
        {
            Comments = post.Comments,
            ApprovedCommentCount = this.CountApprovedComments(post, contextWrapper),
            CommentsOpen = this.AreCommentsOpen(post, contextWrapper)
        }, context);
    }

    /// <summary>
    /// Renders the comment count section
    /// </summary>
    /// <param name="post">The current post</param>
    /// <param name="context">The context</param>
    /// <returns><see cref="HelperResult"/></returns>
    public HelperResult RenderCommentCountSection(Post post, HttpContext context)
    {
        return RenderHelperResult(CommentCountSectionPath, new CommentCount(CountApprovedComments(post, new HttpContextWrapper(context)), post.Url), context);
    }

    /// <summary>
    /// Renders the global section.  This section is put on every page before the closing body tag. 
    /// </summary>
    /// <param name="context">The context</param>
    /// <returns><see cref="HelperResult"/></returns>
    public virtual HelperResult RenderGlobalSection(HttpContext context)
    {
        return RenderHelperResult(GlobalSectionPath, null, context);
    }

    /// <summary>
    /// Gets settings related to the selected blog engine from the web.config and caches them.
    /// Convention: <add key="{name}:{key}" value="VALUE"/>
    /// </summary>
    /// <param name="key">Key to look for.  This key is concatenated with the comment engine name in the format: name:key</param>
    /// <returns>The setting vale</returns>
    public string GetSetting(string settingKey)
    {
        var key = string.Format("{0}:{1}", Name, settingKey);

        // check if setting is cached already
        if (_settings.ContainsKey(key))
        {
            return _settings[key];
        }
        else
        {
            // Fetch from config
            var fromConfiguration = ConfigurationManager.AppSettings.Get(key);

            if (fromConfiguration != null)
            {
                _settings.TryAdd(key, fromConfiguration);
                return fromConfiguration;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Loads the comments and returns them
    /// </summary>
    /// <param name="doc"></param>
    /// <returns></returns>
    public virtual IEnumerable<Comment> LoadComments(XElement doc = null)
    {
        return new List<Comment>();
    }

    /// <summary>
    /// Counts the approved comments
    /// </summary>
    /// <param name="post"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual int CountApprovedComments(Post post, HttpContextBase context)
    {
        return post.Comments.Count(c => c.IsApproved);
    }

    /// <summary>
    /// Whether comments are open
    /// </summary>
    /// <param name="post"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual bool AreCommentsOpen(Post post, HttpContextBase context)
    {
        return true;
    }

    /// <summary>
    /// Renders a view page
    /// </summary>
    /// <param name="pageUrl">Url to render</param>
    /// <param name="model">Model to pass into view</param>
    /// <param name="context">Http Context</param>
    /// <returns><see cref="HelperResult"/></returns>
    protected HelperResult RenderHelperResult(string pageUrl, object model, HttpContext context)
    {   
        if (!File.Exists(context.Server.MapPath(VirtualPathUtility.ToAbsolute(pageUrl))))
        {
            return null;
        }

        return new HelperResult(writer =>
        {
            var page =
                (WebPage)WebPageBase.CreateInstanceFromVirtualPath(pageUrl);
            page.Context = new HttpContextWrapper(context);
            page.ExecutePageHierarchy(new WebPageContext(page.Context, page: null, model: model), writer);
        });
    }
}