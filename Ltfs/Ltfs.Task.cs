using Ltfs.Index;
using Ltfs.Tasks;

namespace Ltfs;

public partial class Ltfs
{
	public bool HasPendingTasks => pendingTasks.Any(IsUncommitted);

	public async Task<bool> Commit()
	{
		var activeTasks = GetUncommittedTasks();
		if (activeTasks.Count == 0)
			return true;

		var executableTasks = activeTasks.Where(IsExecutable).ToArray();
		var workingIndex = (LtfsIndex)GetLatestIndex().Clone();

		foreach (var task in activeTasks)
		{
			UpdateIndexByTask(workingIndex, task);
		}

		foreach (var task in executableTasks.Where(task => task is not WriteTask))
		{
			MarkTaskRunning(task);
			MarkTaskCompleted(task);
		}

		LtfsIndexCurr = workingIndex;

		var writeTasks = executableTasks.OfType<WriteTask>().ToArray();
		if (writeTasks.Length > 0)
		{
			var writeSuccess = await PerformWriteTasks(writeTasks, workingIndex);
			if (!writeSuccess)
				return false;
		}

		WriteLtfsIndex();

		foreach (var task in activeTasks)
		{
			MarkTaskCommitted(task);
		}

		return true;
	}

	private IReadOnlyList<TaskBase> GetUncommittedTasks()
	{
		return pendingTasks
			.Where(IsUncommitted)
			.OrderBy(task => task.SequenceNumber)
			.ToArray();
	}

	private static bool IsUncommitted(TaskBase task)
	{
		return task.Status != TaskExecutionStatus.Committed;
	}

	private static bool IsExecutable(TaskBase task)
	{
		return task.Status is TaskExecutionStatus.Pending or TaskExecutionStatus.Failed;
	}

	private static void MarkTaskRunning(TaskBase task)
	{
		task.Status = TaskExecutionStatus.Running;
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;
	}

	private static void MarkTaskCompleted(TaskBase task)
	{
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;

		task.Status = TaskExecutionStatus.Completed;
		task.EndTime = DateTime.UtcNow;
	}

	private static void MarkTaskFailed(TaskBase task)
	{
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;

		task.Status = TaskExecutionStatus.Failed;
		task.EndTime = DateTime.UtcNow;
	}

	private static void MarkTaskCommitted(TaskBase task)
	{
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;

		task.Status = TaskExecutionStatus.Committed;
		task.EndTime = DateTime.UtcNow;
	}
}
