namespace DataMap.Api.Exceptions;

public class InviteNotFoundException() : Exception("Invite not found.");

public class InviteExpiredException() : Exception("This invite has expired.");

public class InviteUsageExceededException() : Exception("This invite has reached its maximum number of uses.");

public class VersionConflictException() : Exception("The column was modified by someone else. Please refresh and try again.");

public class ValidationException(string message) : Exception(message);

public class TemplateWorkspaceNotFoundException() : Exception("Template workspace not found.");

public class WorkspaceNotFoundException() : Exception("Workspace not found.");
