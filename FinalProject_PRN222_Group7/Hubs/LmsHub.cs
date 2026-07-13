using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace FinalProject_PRN222_Group7.Hubs
{
    public class LmsHub : Hub
    {
        public async Task NotifyCourseUpdate()
        {
            await Clients.All.SendAsync("ReceiveCourseUpdate");
        }

        public async Task NotifyDocumentUpdate(int courseId)
        {
            await Clients.All.SendAsync("ReceiveDocumentUpdate", courseId);
        }
    }
}
