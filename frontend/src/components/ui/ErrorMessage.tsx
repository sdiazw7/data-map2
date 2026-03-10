type Props = {
  message: string
}

export default function ErrorMessage({ message }: Props) {
  return (
    <div className="rounded border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700">
      {message}
    </div>
  )
}
