using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecuringAngularApps.API.Model;

namespace SecuringAngularApps.API.Controllers
{
    [Produces("application/json")]
    [Route("api/Projects")]
    //[Authorize]
    public class ProjectsController : Controller
    {
        private readonly ProjectDbContext _context;

        public ProjectsController(ProjectDbContext context)
        {
            _context = context;
        }

        [HttpGet("AuthContext")]
        [Authorize()]
        public IActionResult GetAuthContext()
        {
            var userId = this.User.FindFirstValue(JwtClaimTypes.Subject);
            var profile = _context.UserProfiles.Include("UserPermissions").FirstOrDefault(u => u.Id == userId);
            if (profile == null) return NotFound();
            var context = new AuthContext
            {
                UserProfile = profile,
                Claims = User.Claims.Select(c => new SimpleClaim { Type = c.Type, Value = c.Value }).ToList()
            };
            return Ok(context);
        }

        // GET: api/Projects
        [HttpGet]
        //[AllowAnonymous]
        public IEnumerable<Project> GetProjects()
        {
            //var claims = (from c in User.Claims select new { c.Type, c.Value }).ToList();
            //claims.ForEach(c => Console.WriteLine($"{c.Type}: {c.Value}"));
            if (User.IsInRole("Admin"))
            {
                return _context.Projects;
            }
            else
            {
                var userId = this.User.FindFirstValue(JwtClaimTypes.Subject);
                List<int> userProjectIds = _context.UserPermissions.Where(up =>
                   up.ProjectId.HasValue && up.UserProfileId == userId).Select(up => up.ProjectId.Value).ToList();
                return _context.Projects.Where(p => userProjectIds.Contains(p.Id));
            }
        }

        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (!await ProjectEditAccessCheck(id, false) && !User.IsInRole("Admin")) return Forbid();
            var project = await _context.Projects
                .Include("UserPermissions")
                .Include("Milestones")
                .FirstOrDefaultAsync(m => m.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            return Ok(project);
        }

        [HttpGet("{id}/Users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetProjectUsers([FromRoute] int id)
        {
            var perms = await _context.UserPermissions.Where(up => up.ProjectId == id).ToListAsync();
            var users = from u in _context.UserProfiles.Include("UserPermissions")
                        join p in perms
                        on u.Id equals p.UserProfileId
                        where p.Value != "Admin"
                        select u;
            return Ok(users);
        }

        // PUT: api/Projects/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProject([FromRoute] int id, [FromBody] Project project)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (id != project.Id)
            {
                return BadRequest();
            }
            if (!await ProjectEditAccessCheck(id, true) && !User.IsInRole("Admin")) return Forbid();
            _context.Entry(project).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProjectExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Projects
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PostProject([FromBody] Project project)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProject", new { id = project.Id }, project);
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProject([FromRoute] int id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var project = await _context.Projects.SingleOrDefaultAsync(m => m.Id == id);
            if (project == null)
            {
                return NotFound();
            }
            var ups = _context.UserPermissions.Where(up => up.ProjectId == id).ToList();
            ups.ForEach(u => _context.UserPermissions.Remove(u));

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return Ok(project);
        }

        [HttpPost("Milestones")]
        public async Task<IActionResult> AddMilestone([FromBody] Milestone milestone)
        {
            var item = await _context.Milestones.FirstOrDefaultAsync(m => m.Id == milestone.Id);
            if (item != null) return StatusCode(409);
            if (!await MilestoneAccessCheck(item) && !User.IsInRole("Admin")) return Forbid();
            _context.Milestones.Add(milestone);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetProject", new { id = milestone.ProjectId }, milestone);
        }

        [HttpDelete("Milestones/{id}")]
        public async Task<IActionResult> DeleteMilestone(int id)
        {
            var item = await _context.Milestones.FirstOrDefaultAsync(m => m.Id == id);
            if (item == null) return NotFound();
            if (!await MilestoneAccessCheck(item) && !User.IsInRole("Admin")) return Forbid();
            _context.Milestones.Remove(item);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("Milestones/{id}")]
        public async Task<IActionResult> UpdateMilestone([FromBody] Milestone milestone, int id)
        {
            if (milestone.Id != id) return BadRequest();
            var item = await _context.Milestones.FirstOrDefaultAsync(ms => ms.Id == id);
            if (item == null) return NotFound();
            if (!await MilestoneAccessCheck(item) && !User.IsInRole("Admin")) return Forbid();
            item.MilestoneStatusId = milestone.MilestoneStatusId;
            item.Name = milestone.Name;
            await _context.SaveChangesAsync();
            return Ok(milestone);
        }

        private async Task<bool> MilestoneAccessCheck(Milestone item)
        {
            var userId = this.User.FindFirstValue(JwtClaimTypes.Subject);
            var perm = await _context.UserPermissions.FirstOrDefaultAsync(up => 
            up.ProjectId == item.ProjectId &&
                up.UserProfileId == userId);
            return (perm != null && perm.Value == "Edit");
        }

        private async Task<bool> ProjectEditAccessCheck(int projectId, bool edit)
        {
            var userId = this.User.FindFirstValue(JwtClaimTypes.Subject);
            var userAccess = await _context.UserPermissions.FirstOrDefaultAsync(up =>
                up.ProjectId == projectId && up.UserProfileId == userId);
            return (userAccess != null && (edit ? userAccess.Value == "Edit" : true));
        }



        [HttpGet("MilestoneStatuses")]
        public IActionResult GetMilestoneStatuses()
        {
            return Ok(_context.MilestoneStatuses);
        }

        private bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.Id == id);
        }

    }
}