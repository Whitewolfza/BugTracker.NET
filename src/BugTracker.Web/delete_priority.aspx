<%@ Page Language="C#" CodeBehind="delete_priority.aspx.cs" Inherits="btnet.delete_priority" MasterPageFile="~/LoggedIn.Master" %>

<%@ MasterType TypeName="btnet.LoggedIn" %>

<asp:Content ContentPlaceHolderID="body" runat="server">
    <div class="align">
        <p>&nbsp</p>
        <a href="priorities.aspx">back to priorities</a>

        <p>
            or<p />


        <form runat="server">
            <a id="confirm_href" runat="server" data-action="submit"></a>
            <input type="hidden" id="row_id" runat="server"/>
        </form>

    </div>
</asp:Content>