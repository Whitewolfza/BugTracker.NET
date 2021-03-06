using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using btnet.Security;

namespace btnet
{
    [PageAuthorize(BtnetRoles.Admin)]
    public partial class edit_priority : BasePage
    {
        protected int id;
        SQLString sql;

        protected void Page_Init(object sender, EventArgs e) { ViewStateUserKey = Session.SessionID; }

        ///////////////////////////////////////////////////////////////////////
        protected void Page_Load(Object sender, EventArgs e)
        {

            Util.do_not_cache(Response);

            Master.Menu.SelectedItem = "admin";
            Page.Header.Title = Util.get_setting("AppTitle", "BugTracker.NET") + " - "
                + "edit priority";

            msg.InnerText = "";

            string var = Request.QueryString["id"];
            if (var == null)
            {
                id = 0;
            }
            else
            {
                id = Convert.ToInt32(var);
            }

            if (!IsPostBack)
            {

                // add or edit?
                if (id == 0)
                {
                    sub.Value = "Create";
                }
                else
                {
                    sub.Value = "Update";

                    // Get this entry's data from the db and fill in the form

                    sql = new SQLString(@"select
				pr_name, pr_sort_seq, pr_background_color, isnull(pr_style,'') [pr_style], pr_default
				from priorities where pr_id = @id");

                    sql = sql.AddParameterWithValue("id", id);
                    DataRow dr = btnet.DbUtil.get_datarow(sql);

                    // Fill in this form
                    name.Value = (string)dr["pr_name"];
                    sort_seq.Value = Convert.ToString((int)dr["pr_sort_seq"]);
                    color.Value = (string)dr["pr_background_color"];
                    style.Value = (string)dr["pr_style"];
                    default_selection.Checked = Convert.ToBoolean((int)dr["pr_default"]);

                }
            }
            else
            {
                on_update();
            }

        }



        ///////////////////////////////////////////////////////////////////////
        Boolean validate()
        {

            Boolean good = true;
            if (name.Value == "")
            {
                good = false;
                name_err.InnerText = "Description is required.";
            }
            else
            {
                name_err.InnerText = "";
            }

            if (sort_seq.Value == "")
            {
                good = false;
                sort_seq_err.InnerText = "Sort Sequence is required.";
            }
            else
            {
                sort_seq_err.InnerText = "";
            }

            if (!Util.is_int(sort_seq.Value))
            {
                good = false;
                sort_seq_err.InnerText = "Sort Sequence must be an integer.";
            }
            else
            {
                sort_seq_err.InnerText = "";
            }


            if (color.Value == "")
            {
                good = false;
                color_err.InnerText = "Background Color in #FFFFFF format is required.";
            }
            else
            {
                color_err.InnerText = "";
            }


            return good;
        }

        ///////////////////////////////////////////////////////////////////////
        void on_update()
        {

            Boolean good = validate();

            if (good)
            {
                if (id == 0)  // insert new
                {
                    sql = new SQLString(@"insert into priorities
				(pr_name, pr_sort_seq, pr_background_color, pr_style, pr_default)
				values (@na, @ss, @co, @st, @df)");
                }
                else // edit existing
                {

                    sql = new SQLString(@"update priorities set
				pr_name = @na,
				pr_sort_seq = @ss,
				pr_background_color = @co,
				pr_style = @st,
				pr_default = @df
				where pr_id = @id");

                    sql = sql.AddParameterWithValue("id", Convert.ToString(id));

                }
                sql = sql.AddParameterWithValue("na", name.Value);
                sql = sql.AddParameterWithValue("ss", sort_seq.Value);
                sql = sql.AddParameterWithValue("co", color.Value);
                sql = sql.AddParameterWithValue("st", style.Value);
                sql = sql.AddParameterWithValue("df", Util.bool_to_string(default_selection.Checked));
                btnet.DbUtil.execute_nonquery(sql);
                Server.Transfer("priorities.aspx");

            }
            else
            {
                if (id == 0)  // insert new
                {
                    msg.InnerText = "Priority was not created.";
                }
                else // edit existing
                {
                    msg.InnerText = "Priority was not updated.";
                }

            }

        }

    }
}
