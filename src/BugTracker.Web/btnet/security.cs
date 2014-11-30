/*
Copyright 2002-2011 Corey Trager
Distributed under the terms of the GNU General Public License
*/

using System;
using System.Web;
using System.Data;
using System.Collections.Specialized;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Claims;

namespace btnet
{

	public class Security {
        public const int PERMISSION_NONE = 0;
        public const int PERMISSION_READONLY = 1;
        public const int PERMISSION_REPORTER = 3;
        public const int PERMISSION_ALL = 2;

        public User user = new User();
        public string auth_method = "";
        public HttpContext context = null;

        static string goto_form = @"
<td nowrap valign=middle>
    <form style='margin: 0px; padding: 0px;' action=edit_bug.aspx method=get>
        <input class=menubtn type=submit value='go to ID'>
        <input class=menuinput size=4 type=text class=txt name=id accesskey=g>
    </form>
</td>";

        public static void SignIn(HttpRequest request, string username)
        {
            SQLString sql = new SQLString(@"
select u.us_id, u.us_username, u.us_org, u.us_bugs_per_page, u.us_enable_bug_list_popups,
       org.*,
       isnull(u.us_forced_project, 0 ) us_forced_project,
       proj.pu_permission_level,
       isnull(proj.pu_admin, 0) pu_admin,
       u.us_admin
from users u
inner join orgs org 
    on u.us_org = org.og_id
left outer join project_user_xref proj
	on proj.pu_project = u.us_forced_project
	and proj.pu_user = u.us_id
where us_username = @us and u.us_active = 1");
            sql = sql.AddParameterWithValue("us", username);
            DataRow dr = btnet.DbUtil.get_datarow(sql);

            var bugsPerPage = string.IsNullOrEmpty(dr["us_bugs_per_page"] as string) ? 10 : (int) dr["us_bugs_per_page"];
            
            var claims = new List<Claim>
            {
                new Claim(BtnetClaimTypes.UserId, Convert.ToString(dr["us_id"])),
                new Claim(ClaimTypes.Name, Convert.ToString(dr["us_username"])),
                new Claim(BtnetClaimTypes.OrganizationId, Convert.ToString(dr["us_org"])),
                new Claim(BtnetClaimTypes.BugsPerPage, Convert.ToString(bugsPerPage)),
                new Claim(BtnetClaimTypes.EnablePopUps, Convert.ToString((int) dr["us_enable_bug_list_popups"] == 1)),
                new Claim(BtnetClaimTypes.CanOnlySeeOwnReportedBugs, Convert.ToString((int) dr["og_can_only_see_own_reported"] == 1)),
                new Claim(BtnetClaimTypes.CanUseReports, Convert.ToString((int) dr["og_can_use_reports"] == 1)),
                new Claim(BtnetClaimTypes.CanEditReports, Convert.ToString((int) dr["og_can_edit_reports"] == 1)),
                new Claim(BtnetClaimTypes.OtherOrgsPermissionLevel, Convert.ToString(dr["og_other_orgs_permission_level"])),
                new Claim(BtnetClaimTypes.CanOnlySeeOwnReportedBugs, Convert.ToString((int) dr["us_enable_bug_list_popups"] == 1)),
                new Claim(BtnetClaimTypes.CanSearch, Convert.ToString((int) dr["og_can_search"] == 1))

            };

            bool canAdd = true;
            int permssionLevel = dr["pu_permission_level"] == DBNull.Value
                ? Convert.ToInt32(Util.get_setting("DefaultPermissionLevel", "2"))
                : (int) dr["pu_permission_level"];
            // if user is forced to a specific project, and doesn't have
            // at least reporter permission on that project, than user
            // can't add bugs
            if ((int)dr["us_forced_project"] != 0)
            {
                if (permssionLevel == Security.PERMISSION_READONLY || permssionLevel  == Security.PERMISSION_NONE)
                {
                    canAdd = false;
                }
            }
            claims.Add(new Claim(BtnetClaimTypes.CanAddBugs, Convert.ToString(canAdd)));

            int tagsPermissionLevel;
            if (Util.get_setting("EnableTags", "0") == "1")
            {
                tagsPermissionLevel = (int)dr["og_tags_field_permission_level"];
            }
            else
            {
                tagsPermissionLevel = Security.PERMISSION_NONE;
            }

            claims.Add(new Claim(BtnetClaimTypes.TagsPermissionLevel, Convert.ToString(tagsPermissionLevel)));


            if ((int) dr["us_admin"] == 1)
            {
                claims.Add(new Claim(ClaimTypes.Role, BtnetRoles.Admin));
            }
            else
            {
                if ((int) dr["project_admin"] > 0)
                {
                    claims.Add(new Claim(ClaimTypes.Role, BtnetRoles.ProjectAdmin));
                }
            }
            claims.Add(new Claim(ClaimTypes.Role, BtnetRoles.User));
            

            var identity = new ClaimsIdentity(claims, "ApplicationCookie", ClaimTypes.Name, ClaimTypes.Role);
            var owinContext = request.GetOwinContext();
            owinContext.Authentication.SignIn(identity);
        }

	    public static void SignOut(HttpRequest request)
	    {
            var owinContext = request.GetOwinContext();
	        owinContext.Authentication.SignOut();
	    }

		///////////////////////////////////////////////////////////////////////
		public static void create_session(HttpRequest Request, HttpResponse Response, int userid, string username, string NTLM)
		{

			// Generate a random session id
			// Don't use a regularly incrementing identity
			// column because that can be guessed.
			string guid = Guid.NewGuid().ToString();

			btnet.Util.write_to_log("guid=" + guid);
			
			var sql = new SQLString(@"insert into sessions (se_id, se_user) values(@gu, @us)");
			sql = sql.AddParameterWithValue("gu", guid);
			sql = sql.AddParameterWithValue("us", Convert.ToString(userid));

			btnet.DbUtil.execute_nonquery(sql);			

			HttpContext.Current.Session[guid] = userid;
			
			string sAppPath = Request.Url.AbsolutePath;
			sAppPath = sAppPath.Substring(0, sAppPath.LastIndexOf('/'));
			Util.write_to_log("AppPath:" + sAppPath);

			Response.Cookies["se_id"].Value = guid;
			Response.Cookies["se_id"].Path = sAppPath;
			Response.Cookies["user"]["name"] = username;
			Response.Cookies["user"]["NTLM"] = NTLM;
			Response.Cookies["user"].Path = sAppPath;
			DateTime dt = DateTime.Now;
			TimeSpan ts = new TimeSpan(365, 0, 0, 0);
			Response.Cookies["user"].Expires = dt.Add(ts);
		}

        ///////////////////////////////////////////////////////////////////////
        public void write_menu_item(HttpResponse Response,
            string this_link, string menu_item, string href)
        {
            Response.Write("<td class='menu_td'>");
            if (this_link == menu_item)
            {
                Response.Write("<a href=" + href + "><span class='selected_menu_item warn'  style='margin-left:3px;'>" + menu_item + "</span></a>");
            }
            else
            {
                Response.Write("<a href=" + href + "><span class='menu_item warn' style='margin-left:3px;'>" + menu_item + "</span></a>");
            }
            Response.Write("</td>");
        }


        ///////////////////////////////////////////////////////////////////////
        public void write_menu(HttpResponse Response, string this_link)
        {

            // topmost visible HTML
            string custom_header = (string)Util.context.Application["custom_header"];
            Response.Write(custom_header);

            Response.Write(@"
<span id=debug style='position:absolute;top:0;left:0;'></span>



<script type='text/javascript' src='scripts/require.js'></script>
<script>
function dbg(s)
{
	document.getElementById('debug').innerHTML += (s + '<br>')
}
function on_submit_search()
{
	el = document.getElementById('lucene_input')
	if (el.value == '')
	{
		alert('Enter the words you are search for.');
		el.focus()
		return false;
	}
	else
	{
		return true;
	}

}

</script>
<script type='text/javascript'>
    require.config({
        baseUrl: 'scripts',
        paths: {
            jquery: 'jquery-1.11.1'
        }
    });
    require(['jquery'], function( $ ) {
        $(function(){
            $('a').filter(function() { return this.hostname && this.hostname !== location.hostname; }).addClass('external-link');
        });
    });
</script>
<table border=0 width=100% cellpadding=0 cellspacing=0 class=menubar><tr>");

            // logo
            string logo = (string)Util.context.Application["custom_logo"];
            Response.Write(logo);

            Response.Write("<td width=20>&nbsp;</td>");
            write_menu_item(Response, this_link, Util.get_setting("PluralBugLabel", "bugs"), "bugs.aspx");
            
            if (user.can_search)
            {
            	write_menu_item(Response, this_link, "search", "search.aspx");
            }

            if (Util.get_setting("EnableWhatsNewPage", "0") == "1")
            {
				write_menu_item(Response, this_link, "news", "view_whatsnew.aspx");
			}

            if (!user.is_guest)
            {
                write_menu_item(Response, this_link, "queries", "queries.aspx");
            }

            if (user.is_admin || user.can_use_reports || user.can_edit_reports)
            {
                write_menu_item(Response, this_link, "reports", "reports.aspx");
            }

            if (Util.get_setting("CustomMenuLinkLabel", "") != "")
            {
                write_menu_item(Response, this_link,
                    Util.get_setting("CustomMenuLinkLabel", ""),
                    Util.get_setting("CustomMenuLinkUrl", ""));
            }

            if (user.is_admin)
            {
                write_menu_item(Response, this_link, "admin", "admin.aspx");
            }
            else if (user.is_project_admin)
            {
                write_menu_item(Response, this_link, "users", "users.aspx");
            }


            // go to

            Response.Write (goto_form);

            // search
            if (Util.get_setting("EnableSearch", "1") == "1" && user.can_search)
            {
                string query = (string) HttpContext.Current.Session["query"];
                if (query == null)
                {
                    query = "";
                }
                string search_form = @"

<td nowrap valign=middle>
    <form style='margin: 0px; padding: 0px;' action=search_text.aspx method=get onsubmit='return on_submit_search()'>
        <input class=menubtn type=submit value='search text'>
        <input class=menuinput  id=lucene_input size=24 type=text class=txt
        value='" + query.Replace("'","") + @"' name=query accesskey=s>
        <a href=lucene_syntax.html target=_blank style='font-size: 7pt;'>advanced</a>
    </form>
</td>";
                //context.Session["query"] = null;
                Response.Write(search_form);
			}

            Response.Write("<td nowrap valign=middle>");
			if (user.is_guest && Util.get_setting("AllowGuestWithoutLogin","0") == "1")
			{
				Response.Write("<span class=smallnote>using as<br>");
			}
			else
			{
				Response.Write("<span class=smallnote>logged in as<br>");
			}
           	Response.Write(user.username);
            Response.Write("</span></td>");

            if (auth_method == "plain")
            {
                if (user.is_guest && Util.get_setting("AllowGuestWithoutLogin","0") == "1")
                {
                	write_menu_item(Response, this_link, "login", "default.aspx");
				}
				else
				{
					write_menu_item(Response, this_link, "logoff", "logoff.aspx");
				}
            }
            
            // for guest account, suppress display of "edit_self
            if (!user.is_guest)
            {
                write_menu_item(Response, this_link, "settings", "edit_self.aspx");
            }


            Response.Write("<td valign=middle align=left'>");
            Response.Write("<a target=_blank href=about.html><span class='menu_item' style='margin-left:3px;'>about</span></a></td>");
            Response.Write("<td nowrap valign=middle>");
            Response.Write("<a target=_blank href=http://ifdefined.com/README.html><span class='menu_item' style='margin-left:3px;'>help</span></a></td>");

            Response.Write("</tr></table><br>");
        }
	} // end Security
}
